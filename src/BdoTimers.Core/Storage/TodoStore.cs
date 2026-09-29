using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

public sealed class TodoStore(JsonFileStore<TodoData> file, TodoData initial, IClock clock)
    : PersistentState<TodoData>(file, initial)
{
    public void Modify(Guid id, Func<TodoList, TodoList> change) => Update(data =>
    {
        var lists = data.Lists.Select(list => list.Id == id ? change(list) : list).ToList();
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
    });

    public Guid CreateList(TodoCadence cadence, AppSettings settings)
    {
        var schedule = Schedule(settings, cadence);
        var list = new TodoList
        {
            Name = "New list",
            Cadence = cadence, Enabled = true, Schedule = schedule,
            NextResetUtc = TodoReset.Next(schedule, clock.UtcNow),
        };
        Update(data => data with { Lists = [.. data.Lists, list] });
        return list.Id;
    }

    public void SetEnabled(Guid id, bool enabled) => Modify(id, list => list.Enabled == enabled ? list : list with { Enabled = enabled });

    public void ApplyDefaultSchedules(AppSettings settings) => Update(data =>
    {
        var lists = data.Lists.Select(list => WithSchedule(list, Schedule(settings, list.Cadence))).ToList();
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
    });

    TodoList WithSchedule(TodoList list, TodoSchedule schedule) => list.Schedule == schedule ? list
        : list with { Schedule = schedule, NextResetUtc = TodoReset.Next(schedule, clock.UtcNow) };

    static TodoSchedule Schedule(AppSettings settings, TodoCadence cadence) =>
        (cadence == TodoCadence.Daily ? settings.DailyTodoReset : settings.WeeklyTodoReset) with { Cadence = cadence };

    public void Reconcile() => Update(data =>
    {
        var now = clock.UtcNow;
        var lists = data.Lists.Select(list =>
        {
            if (list.NextResetUtc == default)
                return list with { NextResetUtc = TodoReset.Next(list.Schedule, now) };
            if (list.NextResetUtc > now) return list;
            return list with
            {
                Rows = list.Rows.Select(row => TodoOps.SetDone(row, false)).ToList(),
                NextResetUtc = TodoReset.Next(list.Schedule, now),
            };
        }).ToList();
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
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
        name = Clean(name);
        Modify(id, list => list with { Name = name });
    }

    public void RenameRow(Guid listId, Guid rowId, string text)
    {
        text = Clean(text);
        Modify(listId, list => list with { Rows = list.Rows.Select(row => RenameIn(row, rowId, text)).ToList() });
    }

    static TodoRow RenameIn(TodoRow row, Guid id, string text) => row.Id == id ? row with { Text = text }
        : row with { Children = row.Children.Select(child => child.Id == id ? child with { Text = text } : child).ToList() };

    public Guid AddRow(Guid listId)
    {
        var row = new TodoRow { Text = "New item" };
        Modify(listId, list => list with { Rows = [.. list.Rows, row] });
        return row.Id;
    }

    public Guid AddChild(Guid listId, Guid parentId)
    {
        var child = new TodoRow { Text = "New step" };
        Modify(listId, list => list with
        {
            Rows = list.Rows.Select(row => row.Id == parentId
                ? row with { Done = false, Children = [.. row.Children, child] } : row).ToList(),
        });
        return child.Id;
    }

    public void DeleteRow(Guid listId, Guid rowId) => Modify(listId, list => list with
    {
        Rows = list.Rows.Where(row => row.Id != rowId).Select(row => row with
        {
            Children = row.Children.Where(child => child.Id != rowId).ToList(),
        }).ToList(),
    });

    public void MoveRow(Guid listId, Guid rowId, int direction) => Modify(listId, list => list with
    {
        Rows = Move(list.Rows, rowId, direction).Select(row => row with
        {
            Children = Move(row.Children, rowId, direction),
        }).ToList(),
    });

    static IReadOnlyList<TodoRow> Move(IReadOnlyList<TodoRow> rows, Guid id, int direction)
    {
        var copy = rows.ToList();
        var index = copy.FindIndex(row => row.Id == id);
        var target = index + direction;
        if (index >= 0 && target >= 0 && target < copy.Count)
            (copy[index], copy[target]) = (copy[target], copy[index]);
        return copy;
    }

    public void Delete(Guid id) => Update(data =>
    {
        var lists = data.Lists.Where(list => list.Id != id || list.IsBuiltIn)
            .Select(list => list.Id == id ? list with { Deleted = true } : list).ToList();
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
    });

    public void RestoreDefaults(AppSettings? settings = null) => Update(data =>
    {
        var source = TodoSeed.Create(clock.UtcNow, settings ?? new AppSettings());
        var lists = data.Lists.Select(list => list.IsBuiltIn && list.Deleted ? list with { Deleted = false } : list).ToList();
        for (var i = 0; i < source.Lists.Count; i++)
            if (lists.All(list => list.Id != source.Lists[i].Id)) lists.Insert(Math.Min(i, lists.Count), source.Lists[i]);
        return lists.SequenceEqual(data.Lists) ? data : data with { Lists = lists };
    });

    static string Clean(string value)
    {
        var text = value.Trim();
        if (text.Length == 0) throw new ArgumentException("Name cannot be empty.", nameof(value));
        return text;
    }
}
