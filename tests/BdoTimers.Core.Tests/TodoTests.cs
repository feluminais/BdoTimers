using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public sealed class TodoTests
{
    [Fact]
    public void Parent_is_derived_and_clicking_partial_parent_finishes_children()
    {
        var a = new TodoRow { Text = "One", Done = true };
        var b = new TodoRow { Text = "Two" };
        var parent = new TodoRow { Text = "Group", Children = [a, b] };
        Assert.Equal(TodoCheck.Partial, TodoOps.Check(parent));
        var checkedParent = TodoOps.Toggle(parent);
        Assert.Equal(TodoCheck.Done, TodoOps.Check(checkedParent));
        Assert.All(checkedParent.Children, child => Assert.True(child.Done));
        Assert.All(TodoOps.Toggle(checkedParent).Children, child => Assert.False(child.Done));
    }

    [Fact]
    public void Checked_rows_sort_last_without_changing_saved_order()
    {
        var rows = new[] { new TodoRow { Text = "A", Done = true }, new TodoRow { Text = "B" }, new TodoRow { Text = "C", Done = true } };
        Assert.Equal(["B", "A", "C"], TodoOps.OpenFirst(rows).Select(r => r.Text));
        Assert.Equal(["A", "B", "C"], rows.Select(r => r.Text));
    }

    [Fact]
    public void Built_in_lists_seed_disabled_and_restore_with_edits()
    {
        var now = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var data = TodoSeed.Create(now, new AppSettings());
        Assert.Equal(2, data.Lists.Count);
        Assert.All(data.Lists, list => Assert.False(list.Enabled));
        using var dir = new TempDir();
        var file = new JsonFileStore<TodoData>(dir.File("todos.json"), () => data);
        var store = new TodoStore(file, data, new FakeClock(now));
        var weekly = data.Lists.Single(l => l.Cadence == TodoCadence.Weekly);
        store.Modify(weekly.Id, l => l with { Name = "Mine" });
        store.Delete(weekly.Id);
        Assert.True(store.Current.Lists.Single(l => l.Id == weekly.Id).Deleted);
        store.RestoreDefaults();
        Assert.Equal("Mine", store.Current.Lists.Single(l => l.Id == weekly.Id).Name);
        Assert.False(store.Current.Lists.Single(l => l.Id == weekly.Id).Deleted);
    }

    [Fact]
    public void Daily_and_weekly_defaults_follow_utc_boundaries()
    {
        var now = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), TodoReset.Next(TodoSchedule.DailyDefault, now));
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), TodoReset.Next(TodoSchedule.WeeklyDefault, now));
    }

    [Fact]
    public void Reconcile_clears_due_checks_even_when_off_and_only_once()
    {
        var now = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        var data = TodoSeed.Create(now, new AppSettings());
        using var dir = new TempDir();
        var file = new JsonFileStore<TodoData>(dir.File("todos.json"), () => data);
        var store = new TodoStore(file, data, clock);
        var daily = data.Lists.Single(l => l.Cadence == TodoCadence.Daily);
        var row = daily.Rows[0];
        store.Modify(daily.Id, l => l with { Rows = [row with { Done = true }, .. l.Rows.Skip(1)] });
        clock.UtcNow = now.AddDays(3);
        store.Reconcile();
        var result = store.Current.Lists.Single(l => l.Id == daily.Id);
        Assert.False(result.Rows[0].Done);
        Assert.True(result.NextResetUtc > clock.UtcNow);
    }

    [Fact]
    public void Changing_reset_schedule_does_not_clear_current_checks()
    {
        var now = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        var data = TodoSeed.Create(now, new AppSettings());
        using var dir = new TempDir();
        var store = new TodoStore(new JsonFileStore<TodoData>(dir.File("todos.json"), () => data), data, clock);
        var daily = data.Lists.Single(l => l.Cadence == TodoCadence.Daily);
        store.Modify(daily.Id, l => l with { Rows = [l.Rows[0] with { Done = true }, .. l.Rows.Skip(1)] });
        store.ApplyDefaultSchedules(new AppSettings { DailyTodoReset = TodoSchedule.DailyDefault with { Hour = 12 } });
        Assert.True(store.Current.Lists.Single(l => l.Id == daily.Id).Rows[0].Done);
    }
}
