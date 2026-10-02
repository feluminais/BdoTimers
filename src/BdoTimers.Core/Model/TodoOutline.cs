namespace BdoTimers.Core.Model;

/// <summary>An editor row; Level is zero for a list item and one for a child.</summary>
public sealed record TodoOutlineRow(Guid Id, string Text, bool Done, int Level);

/// <summary>Pure transformations for the editable one-level outline.</summary>
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

    public static bool TryIndent(List<TodoOutlineRow> rows, int index)
    {
        if (index <= 0 || index >= rows.Count || rows[index].Level != 0 ||
            index + 1 < rows.Count && rows[index + 1].Level == 1) return false;
        rows[index] = rows[index] with { Level = 1 };
        return true;
    }

    public static bool TryOutdent(List<TodoOutlineRow> rows, int index)
    {
        if (index < 0 || index >= rows.Count || rows[index].Level != 1) return false;
        var row = rows[index] with { Level = 0 };
        rows.RemoveAt(index);
        var afterGroup = index;
        while (afterGroup < rows.Count && rows[afterGroup].Level == 1) afterGroup++;
        rows.Insert(afterGroup, row);
        return true;
    }

    public static int InsertAfter(List<TodoOutlineRow> rows, int index, Guid? newId = null)
    {
        var source = rows[index];
        var hasChildren = source.Level == 0 && index + 1 < rows.Count && rows[index + 1].Level == 1;
        var target = index + 1;
        var child = source.Level == 1 || hasChildren;
        rows.Insert(target, new TodoOutlineRow(newId ?? Guid.NewGuid(), "", false, child ? 1 : 0));
        if (hasChildren) rows[index] = source with { Done = false };
        return target;
    }

    public static bool TryMove(List<TodoOutlineRow> rows, int index, int direction)
    {
        if (index < 0 || index >= rows.Count || direction is not (-1 or 1)) return false;
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

    public static void Remove(List<TodoOutlineRow> rows, int index)
    {
        var end = index + 1;
        if (rows[index].Level == 0) while (end < rows.Count && rows[end].Level == 1) end++;
        rows.RemoveRange(index, end - index);
    }
}
