namespace BdoTimers.Core.Model;

/// <summary>An editor row; Level is zero for a list item and one for a child.</summary>
public sealed record TodoOutlineRow(Guid Id, string Text, bool Done, int Level);

/// <summary>
/// Pure transformations for the editable one-level outline. An item given a child is no longer done: only rows without
/// children keep a done flag.
/// </summary>
public static class TodoOutline
{
    public static List<TodoOutlineRow> Flatten(IEnumerable<TodoRow> rows)
    {
        var result = new List<TodoOutlineRow>();
        foreach (var row in rows)
        {
            result.Add(new(row.Id, row.Text, row.Done, 0));
            result.AddRange(row.Children.Select(child => new TodoOutlineRow(child.Id, child.Text, child.Done, 1)));
        }
        return result;
    }

    public static IReadOnlyList<TodoRow> Build(IReadOnlyList<TodoOutlineRow> rows)
    {
        var result = new List<TodoRow>();
        TodoOutlineRow? parent = null;
        var children = new List<TodoRow>();
        void Flush()
        {
            if (parent is null) return;
            if (string.IsNullOrWhiteSpace(parent.Text)) result.AddRange(children);
            else result.Add(new TodoRow
            {
                Id = parent.Id, Text = parent.Text.Trim(), Done = children.Count == 0 && parent.Done,
                Children = children.ToList(),
            });
            children.Clear();
        }

        foreach (var item in rows)
        {
            if (item.Level != 1 || parent is null)
            {
                Flush();
                parent = item;
            }
            else if (!string.IsNullOrWhiteSpace(item.Text))
                children.Add(new TodoRow { Id = item.Id, Text = item.Text.Trim(), Done = item.Done });
        }
        Flush();
        return result;
    }

    /// <summary>Take fresh completion flags from storage without replacing unfinished editor structure.</summary>
    public static List<TodoOutlineRow> MergeChecks(IReadOnlyList<TodoOutlineRow> staged, IReadOnlyList<TodoRow> stored)
    {
        var checks = Flatten(stored).ToDictionary(row => row.Id, row => row.Done);
        return staged.Select(row => checks.TryGetValue(row.Id, out var done)
            ? row with { Done = done } : row).ToList();
    }

    /// <summary>How many children the row at <paramref name="index"/> has; none for a child.</summary>
    public static int CountChildren(IReadOnlyList<TodoOutlineRow> rows, int index)
    {
        if (rows[index].Level != 0) return 0;
        var end = index + 1;
        while (end < rows.Count && rows[end].Level == 1) end++;
        return end - index - 1;
    }

    /// <summary>The id of the item a child belongs to; null for an item.</summary>
    public static Guid? ParentId(IReadOnlyList<TodoOutlineRow> rows, int index) =>
        rows[index].Level == 0 ? null : rows[ItemAt(rows, index)].Id;

    /// <summary>Tab: an item without children, below another row, becomes a child of the item above.</summary>
    public static bool CanIndent(IReadOnlyList<TodoOutlineRow> rows, int index) =>
        index > 0 && index < rows.Count && rows[index].Level == 0 && CountChildren(rows, index) == 0;

    public static bool TryIndent(List<TodoOutlineRow> rows, int index)
    {
        if (!CanIndent(rows, index)) return false;
        rows[index] = rows[index] with { Level = 1 };
        NotDone(rows, ItemAt(rows, index));
        return true;
    }

    /// <summary>Shift+Tab: a child becomes an item, placed after the rest of its group.</summary>
    public static bool CanOutdent(IReadOnlyList<TodoOutlineRow> rows, int index) => InRange(rows, index) && rows[index].Level == 1;

    public static bool TryOutdent(List<TodoOutlineRow> rows, int index)
    {
        if (!CanOutdent(rows, index)) return false;
        var row = rows[index] with { Level = 0 };
        rows.RemoveAt(index);
        var afterGroup = index;
        while (afterGroup < rows.Count && rows[afterGroup].Level == 1) afterGroup++;
        rows.Insert(afterGroup, row);
        return true;
    }

    /// <summary>
    /// Enter: a new empty row with <paramref name="id"/> below the row at <paramref name="index"/>, a child when that row
    /// is a child or has children.
    /// </summary>
    public static void InsertAfter(List<TodoOutlineRow> rows, int index, Guid id)
    {
        if (!InRange(rows, index)) return;
        var child = rows[index].Level == 1 || CountChildren(rows, index) > 0;
        rows.Insert(index + 1, new TodoOutlineRow(id, "", false, child ? 1 : 0));
        if (child && rows[index].Level == 0) NotDone(rows, index);
    }

    /// <summary>A new empty child with <paramref name="id"/> after the other children of the item at <paramref name="index"/>.</summary>
    public static void AddChild(List<TodoOutlineRow> rows, int index, Guid id)
    {
        if (!InRange(rows, index) || rows[index].Level != 0) return;
        rows.Insert(index + 1 + CountChildren(rows, index), new TodoOutlineRow(id, "", false, 1));
        NotDone(rows, index);
    }

    public static bool TryMove(List<TodoOutlineRow> rows, int index, int direction)
    {
        if (!InRange(rows, index) || direction is not (-1 or 1)) return false;
        var level = rows[index].Level;
        var end = index + 1;
        if (level == 0) while (end < rows.Count && rows[end].Level == 1) end++;
        var block = rows.GetRange(index, end - index);
        if (direction < 0)
        {
            var previous = index - 1;
            if (previous < 0 || level == 1 && rows[previous].Level != 1) return false;
            if (level == 0) while (previous > 0 && rows[previous].Level == 1) previous--;
            rows.RemoveRange(index, block.Count);
            rows.InsertRange(previous, block);
        }
        else
        {
            if (end >= rows.Count || level == 1 && rows[end].Level != 1) return false;
            var nextEnd = end + 1;
            if (level == 0) while (nextEnd < rows.Count && rows[nextEnd].Level == 1) nextEnd++;
            rows.RemoveRange(index, block.Count);
            rows.InsertRange(nextEnd - block.Count, block);
        }
        return true;
    }

    /// <summary>Dragging: a row can take the place of another row of the same level under the same item.</summary>
    public static bool CanMoveTo(IReadOnlyList<TodoOutlineRow> rows, int source, int target) =>
        InRange(rows, source) && InRange(rows, target) && source != target
        && rows[source].Level == rows[target].Level && ParentId(rows, source) == ParentId(rows, target);

    /// <summary>Moves the row at <paramref name="source"/>, with its children, one place at a time until it has passed
    /// the row at <paramref name="target"/>.</summary>
    public static void MoveTo(List<TodoOutlineRow> rows, int source, int target)
    {
        if (!CanMoveTo(rows, source, target)) return;
        var (sourceId, targetId) = (rows[source].Id, rows[target].Id);
        var direction = source < target ? 1 : -1;
        for (var tries = 0; tries < rows.Count; tries++)
        {
            var from = rows.FindIndex(r => r.Id == sourceId);
            var to = rows.FindIndex(r => r.Id == targetId);
            if (direction > 0 ? from > to : from < to) break;
            if (!TryMove(rows, from, direction)) break;
        }
    }

    public static void Remove(List<TodoOutlineRow> rows, int index)
    {
        var end = index + 1;
        if (rows[index].Level == 0) while (end < rows.Count && rows[end].Level == 1) end++;
        rows.RemoveRange(index, end - index);
    }

    static bool InRange(IReadOnlyList<TodoOutlineRow> rows, int index) => index >= 0 && index < rows.Count;

    /// <summary>The index of the item the row at <paramref name="index"/> is, or belongs to.</summary>
    static int ItemAt(IReadOnlyList<TodoOutlineRow> rows, int index)
    {
        while (index > 0 && rows[index].Level == 1) index--;
        return index;
    }

    static void NotDone(List<TodoOutlineRow> rows, int index) => rows[index] = rows[index] with { Done = false };
}
