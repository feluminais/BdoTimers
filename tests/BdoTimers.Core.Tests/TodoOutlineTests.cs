using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public sealed class TodoOutlineTests
{
    static TodoOutlineRow Item(string text, bool done = false) => new(Guid.NewGuid(), text, done, 0);
    static TodoOutlineRow Child(string text, bool done = false) => new(Guid.NewGuid(), text, done, 1);
    static string[] Shape(IEnumerable<TodoOutlineRow> rows) => rows.Select(r => new string('-', r.Level) + r.Text).ToArray();

    [Fact]
    public void Children_and_parents_are_found_from_the_rows_order()
    {
        List<TodoOutlineRow> rows = [Item("A"), Child("A1"), Child("A2"), Item("B")];

        Assert.Equal([2, 0, 0, 0], Enumerable.Range(0, rows.Count).Select(i => TodoOutline.CountChildren(rows, i)));
        Assert.Equal([null, rows[0].Id, rows[0].Id, null], Enumerable.Range(0, rows.Count).Select(i => TodoOutline.ParentId(rows, i)));
    }

    [Fact]
    public void Enter_adds_a_child_under_a_child_or_an_item_with_children_and_an_item_otherwise()
    {
        List<TodoOutlineRow> rows = [Item("A", done: true), Item("B", done: true), Child("B1")];

        TodoOutline.InsertAfter(rows, 2, Guid.NewGuid());
        TodoOutline.InsertAfter(rows, 1, Guid.NewGuid());
        TodoOutline.InsertAfter(rows, 0, Guid.NewGuid());
        TodoOutline.InsertAfter(rows, -1, Guid.NewGuid());

        Assert.Equal(["A", "", "B", "-", "-B1", "-"], Shape(rows));
        Assert.True(rows[0].Done);
        Assert.False(rows[2].Done);
    }

    [Fact]
    public void Indent_makes_the_item_above_a_parent_that_is_no_longer_done()
    {
        List<TodoOutlineRow> rows = [Item("A", done: true), Child("A1"), Item("B"), Item("C", done: true), Child("C1")];

        Assert.False(TodoOutline.CanIndent(rows, 0));
        Assert.False(TodoOutline.CanIndent(rows, 1));
        Assert.False(TodoOutline.CanIndent(rows, 3));
        Assert.True(TodoOutline.TryIndent(rows, 2));

        Assert.Equal(["A", "-A1", "-B", "C", "-C1"], Shape(rows));
        Assert.False(rows[0].Done);
        Assert.True(rows[3].Done);
        Assert.False(TodoOutline.CanOutdent(rows, 0));
        Assert.True(TodoOutline.CanOutdent(rows, 2));
    }

    [Fact]
    public void A_new_child_goes_after_the_item_s_other_children()
    {
        List<TodoOutlineRow> rows = [Item("A", done: true), Child("A1"), Item("B")];
        var id = Guid.NewGuid();

        TodoOutline.AddChild(rows, 0, id);
        TodoOutline.AddChild(rows, 1, Guid.NewGuid());

        Assert.Equal(["A", "-A1", "-", "B"], Shape(rows));
        Assert.Equal(id, rows[2].Id);
        Assert.False(rows[0].Done);
    }

    [Fact]
    public void Rows_move_to_the_place_of_a_sibling_with_their_children()
    {
        List<TodoOutlineRow> rows = [Item("A"), Child("A1"), Child("A2"), Item("B"), Item("C")];

        Assert.False(TodoOutline.CanMoveTo(rows, 1, 3));
        Assert.False(TodoOutline.CanMoveTo(rows, 0, 0));
        Assert.False(TodoOutline.CanMoveTo(rows, -1, 0));
        TodoOutline.MoveTo(rows, 1, 3);
        Assert.Equal(["A", "-A1", "-A2", "B", "C"], Shape(rows));

        TodoOutline.MoveTo(rows, 0, 4);
        Assert.Equal(["B", "C", "A", "-A1", "-A2"], Shape(rows));
        TodoOutline.MoveTo(rows, 3, 4);
        Assert.Equal(["B", "C", "A", "-A2", "-A1"], Shape(rows));
        TodoOutline.MoveTo(rows, 2, 0);
        Assert.Equal(["A", "-A2", "-A1", "B", "C"], Shape(rows));
    }

    [Fact]
    public void Outline_keeps_checks_and_promotes_children_of_a_blank_parent()
    {
        var parent = new TodoRow { Text = "Group", Children = [new TodoRow { Text = "Child", Done = true }] };
        var rows = TodoOutline.Flatten([parent]);
        rows[0] = rows[0] with { Text = "" };
        Assert.Equal("Child", TodoOutline.Build(rows).Single().Text);
        Assert.True(TodoOutline.Build(rows).Single().Done);
        rows[0] = rows[0] with { Text = "Group" };
        Assert.True(TodoOutline.Build(rows).Single().Children.Single().Done);
    }

    [Fact]
    public void Reset_check_merge_preserves_unfinished_parent_and_child_position()
    {
        var child = new TodoRow { Text = "Child", Done = true };
        var blankParent = new TodoOutlineRow(Guid.NewGuid(), "", false, 0);
        var staged = new List<TodoOutlineRow> { blankParent, new(child.Id, child.Text, child.Done, 1) };

        var merged = TodoOutline.MergeChecks(staged, [child with { Done = false }]);

        Assert.Equal(blankParent, merged[0]);
        Assert.Equal(1, merged[1].Level);
        Assert.False(merged[1].Done);
        Assert.Equal("Child", TodoOutline.Build(merged).Single().Text);
    }

    [Fact]
    public void Outline_indent_outdent_and_move_preserve_groups()
    {
        var rows = TodoOutline.Flatten([
            new TodoRow { Text = "A" }, new TodoRow { Text = "B" }, new TodoRow { Text = "C" },
        ]);
        Assert.False(TodoOutline.TryIndent(rows, 0));
        Assert.True(TodoOutline.TryIndent(rows, 1));
        Assert.Equal(1, rows[1].Level);
        Assert.True(TodoOutline.TryOutdent(rows, 1));
        Assert.Equal(0, rows[1].Level);
        Assert.True(TodoOutline.TryMove(rows, 0, 1));
        Assert.Equal(["B", "A", "C"], rows.Select(r => r.Text));
    }

    [Fact]
    public void Outline_enter_and_remove_keep_one_level_and_checks()
    {
        var parent = new TodoRow { Text = "Parent", Children = [new TodoRow { Text = "First", Done = true }] };
        var rows = TodoOutline.Flatten([parent, new TodoRow { Text = "Second" }]);
        var id = Guid.NewGuid();
        TodoOutline.InsertAfter(rows, 0, id);
        var first = rows.FindIndex(r => r.Id == id);
        Assert.Equal(1, first);
        Assert.Equal(1, rows[first].Level);
        rows[first] = rows[first] with { Text = "New" };
        Assert.Equal(["New", "First"], TodoOutline.Build(rows)[0].Children.Select(r => r.Text));
        Assert.True(TodoOutline.Build(rows)[0].Children[1].Done);
        Assert.False(TodoOutline.TryIndent(rows, 0));
        Assert.True(TodoOutline.TryOutdent(rows, first));
        Assert.Equal(["Parent", "New", "Second"], TodoOutline.Build(rows).Select(r => r.Text));
        TodoOutline.Remove(rows, 0);
        Assert.Equal(["New", "Second"], TodoOutline.Build(rows).Select(r => r.Text));
    }
}
