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
}
