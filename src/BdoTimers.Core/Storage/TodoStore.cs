using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

public sealed class TodoStore(JsonFileStore<TodoData> file, TodoData initial, IClock clock)
    : PersistentState<TodoData>(file, initial)
{
    /// <summary>The name a list starts with; an untouched new list still has it.</summary>
    public const string NewListName = "New list";

    public void Modify(Guid id, Func<TodoList, TodoList> change) =>
        UpdateLists(lists => lists.Select(list => list.Id == id ? change(list) : list).ToList());

    public Guid CreateList(TodoCadence cadence, AppSettings settings)
    {
        var list = new TodoList
        {
            Name = NewListName,
            Cadence = cadence, Enabled = true,
            NextResetUtc = TodoReset.Next(cadence, Schedule(settings, cadence), clock.UtcNow),
        };
        Update(data => data with { Lists = [.. data.Lists, list] });
        return list.Id;
    }

    public void SetEnabled(Guid id, bool enabled) => Modify(id, list => list with { Enabled = enabled });

    static TodoSchedule Schedule(AppSettings settings, TodoCadence cadence) =>
        cadence == TodoCadence.Daily ? settings.DailyTodoReset : settings.WeeklyTodoReset;

    /// <summary>
    /// Clears the lists whose reset is due and moves every list's next reset to its cadence's schedule in
    /// <paramref name="settings"/>, so a changed schedule applies without clearing checks and a reset set while the clock
    /// ran ahead comes back instead of skipping resets.
    /// </summary>
    public void Reconcile(AppSettings settings) => UpdateLists(lists =>
    {
        var now = clock.UtcNow;
        return lists.Select(list =>
        {
            var due = list.NextResetUtc != default && list.NextResetUtc <= now;
            return list with
            {
                Rows = due ? list.Rows.Select(row => TodoOps.SetDone(row, false)).ToList() : list.Rows,
                NextResetUtc = TodoReset.Next(list.Cadence, Schedule(settings, list.Cadence), now),
            };
        }).ToList();
    });

    public void Toggle(Guid listId, Guid rowId) => Modify(listId, list =>
    {
        if (!list.Enabled || list.Deleted || !list.Rows.Any(row => row.Id == rowId || row.Children.Any(c => c.Id == rowId))) return list;
        return list with { Rows = list.Rows.Select(row => ToggleIn(row, rowId)).ToList() };
    });

    public void ReplaceRows(Guid listId, IReadOnlyList<TodoRow> rows) => Modify(listId,
        list => list.Deleted ? list : list with { Rows = rows });

    static TodoRow ToggleIn(TodoRow row, Guid id) => row.Id == id ? TodoOps.Toggle(row)
        : row with { Children = row.Children.Select(child => child.Id == id ? TodoOps.Toggle(child) : child).ToList() };

    public void Rename(Guid id, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name cannot be empty.", nameof(name));
        Modify(id, list => list with { Name = trimmed });
    }

    public void Delete(Guid id) => DeleteForUndo(id);

    internal DeletedTodoList? DeleteForUndo(Guid id)
    {
        DeletedTodoList? deleted = null;
        UpdateLists(lists =>
        {
            var index = lists.ToList().FindIndex(l => l.Id == id && !l.Deleted);
            if (index < 0) return lists;
            deleted = new(lists[index], index);
            return lists.Where(list => list.Id != id || list.IsBuiltIn)
                .Select(list => list.Id == id ? list with { Deleted = true } : list).ToList();
        });
        return deleted;
    }

    internal bool RestoreDeleted(DeletedTodoList deleted)
    {
        var restored = false;
        UpdateLists(lists =>
        {
            var existing = lists.FirstOrDefault(l => l.Id == deleted.List.Id);
            if (existing is not null && (!deleted.List.IsBuiltIn || !existing.IsBuiltIn || !existing.Deleted)) return lists;
            var next = lists.Where(l => l.Id != deleted.List.Id).ToList();
            next.Insert(Math.Min(deleted.Index, next.Count), deleted.List);
            restored = true;
            return next;
        });
        return restored;
    }

    public void RestoreDefaults(AppSettings settings) => UpdateLists(current =>
    {
        var source = TodoSeed.Create(clock.UtcNow, settings);
        var lists = current.Select(list => list.IsBuiltIn && list.Deleted ? list with { Deleted = false } : list).ToList();
        for (var i = 0; i < source.Lists.Count; i++)
            if (lists.All(list => list.Id != source.Lists[i].Id)) lists.Insert(Math.Min(i, lists.Count), source.Lists[i]);
        return lists;
    });

    /// <summary>Saves the lists <paramref name="change"/> returns, unless they equal the current ones.</summary>
    void UpdateLists(Func<IReadOnlyList<TodoList>, IReadOnlyList<TodoList>> change) => Update(data =>
    {
        var lists = change(data.Lists);
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
    });
}
