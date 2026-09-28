namespace BdoTimers.Core.Model;

public static class TodoOps
{
    public static TodoCheck Check(TodoRow row)
    {
        if (row.Children.Count == 0) return row.Done ? TodoCheck.Done : TodoCheck.Open;
        if (row.Children.All(c => Check(c) == TodoCheck.Done)) return TodoCheck.Done;
        return row.Children.Any(c => Check(c) != TodoCheck.Open) ? TodoCheck.Partial : TodoCheck.Open;
    }

    public static TodoRow Toggle(TodoRow row)
    {
        var done = Check(row) != TodoCheck.Done;
        return SetDone(row, done);
    }

    public static TodoRow SetDone(TodoRow row, bool done) => row with
    {
        Done = row.Children.Count == 0 && done,
        Children = row.Children.Select(c => SetDone(c, done)).ToList(),
    };

    public static IReadOnlyList<TodoRow> OpenFirst(IEnumerable<TodoRow> rows) =>
        rows.OrderBy(r => Check(r) == TodoCheck.Done ? 1 : 0).ToList();

    public static (int Done, int Total) Progress(IEnumerable<TodoRow> rows)
    {
        var leaves = rows.SelectMany(Leaves).ToList();
        return (leaves.Count(r => r.Done), leaves.Count);
    }

    static IEnumerable<TodoRow> Leaves(TodoRow row) => row.Children.Count == 0
        ? [row]
        : row.Children.SelectMany(Leaves);
}
