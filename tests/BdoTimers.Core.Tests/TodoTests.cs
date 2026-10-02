using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public sealed class TodoTests : IDisposable
{
    static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(Now);

    public void Dispose() => _dir.Dispose();

    /// <summary>A store saving to todos.json, holding <paramref name="data"/> or else the shipped lists at the clock's time.</summary>
    TodoStore Store(TodoData? data = null)
    {
        data ??= TodoSeed.Create(_clock.UtcNow, new AppSettings());
        return new TodoStore(new JsonFileStore<TodoData>(_dir.File("todos.json"), () => new TodoData()), data, _clock);
    }

    TodoData Saved() => new JsonFileStore<TodoData>(_dir.File("todos.json"), () => new TodoData()).Load().Value;

    static TodoList CheckFirstRow(TodoList list) => list with { Rows = [list.Rows[0] with { Done = true }, .. list.Rows.Skip(1)] };

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
    public void Daily_and_weekly_defaults_follow_utc_boundaries()
    {
        var now = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), TodoReset.Next(TodoCadence.Daily, new TodoSchedule(), now));
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), TodoReset.Next(TodoCadence.Weekly, new TodoSchedule(), now));
    }

    [Fact]
    public void Reset_uses_shared_dst_rule_and_recovers_invalid_weekday()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var spring = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
        var schedule = new TodoSchedule { Hour = 2, Minute = 30, LocalTime = true };
        Assert.Equal(ScheduleMath.LocalToUtc(new DateTime(2026, 3, 29, 2, 30, 0), berlin),
            TodoReset.Next(TodoCadence.Daily, schedule, spring, berlin));

        var invalid = new TodoSchedule { Day = (DayOfWeek)99 };
        Assert.Equal(TodoReset.Next(TodoCadence.Weekly, new TodoSchedule(), Now), TodoReset.Next(TodoCadence.Weekly, invalid, Now));
        var invalidTime = new TodoSchedule { Hour = 99, Minute = -1 };
        Assert.Equal(TodoReset.Next(TodoCadence.Daily, new TodoSchedule(), Now), TodoReset.Next(TodoCadence.Daily, invalidTime, Now));
    }

    [Fact]
    public void New_lists_are_on_and_empty_and_seed_labels_match_spec()
    {
        var store = Store();
        var data = store.Current;
        Assert.Equal("Weekly quests", data.Lists[0].Name);
        Assert.Equal("Daily tasks", data.Lists[1].Name);
        Assert.Equal([
            "Liana / Ludowig daily life skill quest",
            "Imperial Delivery",
            "Pit of the Undying",
        ], data.Lists[1].Rows.Select(row => row.Text));
        Assert.Equal(4, data.Lists[0].Rows.Single(r => r.Text == "Olvia Academy").Children.Count);
        var id = store.CreateList(TodoCadence.Daily, new AppSettings());
        var list = store.Current.Lists.Single(l => l.Id == id);
        Assert.Equal("New list", list.Name);
        Assert.True(list.Enabled);
        Assert.Empty(list.Rows);
    }

    [Fact]
    public void Built_in_lists_seed_disabled_and_restore_with_edits()
    {
        var store = Store();
        Assert.Equal(2, store.Current.Lists.Count);
        Assert.All(store.Current.Lists, list => Assert.False(list.Enabled));
        var weekly = store.Current.Lists.Single(l => l.Cadence == TodoCadence.Weekly);
        store.Modify(weekly.Id, l => l with { Name = "Mine" });
        store.Delete(weekly.Id);
        Assert.True(store.Current.Lists.Single(l => l.Id == weekly.Id).Deleted);
        store.RestoreDefaults(new AppSettings());
        Assert.Equal("Mine", store.Current.Lists.Single(l => l.Id == weekly.Id).Name);
        Assert.False(store.Current.Lists.Single(l => l.Id == weekly.Id).Deleted);
    }

    [Fact]
    public void Restore_recreates_a_missing_default_off_and_leaves_custom_lists()
    {
        var seed = TodoSeed.Create(Now, new AppSettings());
        var store = Store(seed with { Lists = [seed.Lists[0]] });
        var id = store.CreateList(TodoCadence.Weekly, new AppSettings());
        store.RestoreDefaults(new AppSettings());

        Assert.Contains(store.Current.Lists, l => l.Id == TodoSeed.DailyId && !l.Enabled && !l.Deleted);
        Assert.Contains(store.Current.Lists, l => l.Id == id);
        Assert.Equal([TodoSeed.WeeklyId, TodoSeed.DailyId], store.Current.Lists.Where(l => l.IsBuiltIn).Select(l => l.Id));
    }

    [Fact]
    public void Reconcile_clears_due_checks_even_when_off_and_only_once()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero);
        var store = Store();
        store.Modify(TodoSeed.DailyId, CheckFirstRow);
        _clock.UtcNow = _clock.UtcNow.AddDays(3);
        store.Reconcile(new AppSettings());
        var result = store.Current.Lists.Single(l => l.Id == TodoSeed.DailyId);
        Assert.False(result.Rows[0].Done);
        Assert.True(result.NextResetUtc > _clock.UtcNow);
    }

    [Fact]
    public void Reconcile_brings_a_reset_set_while_the_clock_ran_ahead_back_to_the_schedule()
    {
        _clock.UtcNow = Now.AddDays(5);
        var store = Store();
        store.Modify(TodoSeed.DailyId, CheckFirstRow);
        _clock.UtcNow = Now;

        store.Reconcile(new AppSettings());

        var result = store.Current.Lists.Single(l => l.Id == TodoSeed.DailyId);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero), result.NextResetUtc);
        Assert.True(result.Rows[0].Done);
        var changes = 0;
        store.Changed += () => changes++;
        store.Reconcile(new AppSettings());
        Assert.Equal(0, changes);
        _clock.UtcNow = result.NextResetUtc;
        store.Reconcile(new AppSettings());
        Assert.False(store.Current.Lists.Single(l => l.Id == TodoSeed.DailyId).Rows[0].Done);
    }

    [Fact]
    public void A_changed_schedule_moves_every_list_of_its_cadence_without_clearing_checks()
    {
        var store = Store();
        var customId = store.CreateList(TodoCadence.Weekly, new AppSettings());
        store.Modify(TodoSeed.WeeklyId, CheckFirstRow);
        store.Modify(TodoSeed.DailyId, CheckFirstRow);
        var changed = new AppSettings
        {
            WeeklyTodoReset = new TodoSchedule { Day = DayOfWeek.Friday, Hour = 12 },
            DailyTodoReset = new TodoSchedule { Hour = 12 },
        };

        store.Reconcile(changed);

        var weekly = store.Current.Lists.Where(l => l.Cadence == TodoCadence.Weekly).ToList();
        Assert.Equal(2, weekly.Count);
        Assert.Contains(weekly, l => l.Id == customId);
        Assert.All(weekly, l => Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), l.NextResetUtc));
        Assert.True(weekly.Single(l => l.Id == TodoSeed.WeeklyId).Rows[0].Done);
        var daily = store.Current.Lists.Single(l => l.Id == TodoSeed.DailyId);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), daily.NextResetUtc);
        Assert.True(daily.Rows[0].Done);
    }

    [Fact]
    public void Store_save_failure_keeps_memory_and_file_unchanged()
    {
        var store = Store();
        store.SetEnabled(TodoSeed.DailyId, true);
        var before = store.Current;
        Directory.CreateDirectory(_dir.File("todos.json.tmp"));

        Assert.Throws<StateSaveException>(() => store.Toggle(TodoSeed.DailyId, before.Lists[1].Rows[0].Id));
        Assert.Same(before, store.Current);
        Assert.False(Saved().Lists[1].Rows[0].Done);
    }

    [Fact]
    public void Lists_checks_children_and_next_reset_survive_reload()
    {
        var store = Store();
        var id = store.CreateList(TodoCadence.Weekly, new AppSettings());
        store.ReplaceRows(id, [new TodoRow { Text = "Parent", Children = [new TodoRow { Text = "Child" }] }]);
        store.Toggle(id, store.Current.Lists.Single(l => l.Id == id).Rows[0].Children[0].Id);

        var list = Saved().Lists.Single(l => l.Id == id);
        Assert.True(list.Rows[0].Children[0].Done);
        Assert.Equal(TodoCheck.Done, TodoOps.Check(list.Rows[0]));
        Assert.Equal(TodoCadence.Weekly, list.Cadence);
        Assert.Equal(TodoReset.Next(TodoCadence.Weekly, new TodoSchedule(), Now), list.NextResetUtc);
    }

    [Fact]
    public void Old_daily_defaults_update_once_without_losing_custom_rows_or_checks()
    {
        var seed = TodoSeed.Create(Now, new AppSettings());
        var imperial = new TodoRow { Text = "Imperial Cooking or Alchemy delivery", Done = true };
        var custom = new TodoRow { Text = "My daily task", Done = true };
        var otherList = new TodoList
        {
            Name = "Custom", Rows = [new TodoRow { Text = "Claim login and Challenge (Y) rewards" }],
        };
        var oldDaily = seed.Lists[1] with
        {
            Enabled = true,
            Rows = [custom, new TodoRow { Text = "Claim login and Challenge (Y) rewards" }, imperial],
        };
        var store = Store(seed with { DefaultsVersion = 0, Lists = [seed.Lists[0], oldDaily, otherList] });

        store.Update(TodoMigrations.Apply);

        var migrated = Saved();
        Assert.Equal(TodoData.CurrentDefaultsVersion, migrated.DefaultsVersion);
        var daily = migrated.Lists.Single(list => list.Id == TodoSeed.DailyId);
        Assert.True(daily.Enabled);
        Assert.Equal(["My daily task", "Imperial Delivery"], daily.Rows.Select(row => row.Text));
        Assert.Equal(custom.Id, daily.Rows[0].Id);
        Assert.True(daily.Rows[0].Done);
        Assert.Equal(imperial.Id, daily.Rows[1].Id);
        Assert.True(daily.Rows[1].Done);
        Assert.Equal("Claim login and Challenge (Y) rewards", migrated.Lists[2].Rows[0].Text);

        var readded = migrated with
        {
            Lists = migrated.Lists.Select(list => list.Id == TodoSeed.DailyId
                ? list with { Rows = [.. list.Rows, new TodoRow { Text = "Claim login and Challenge (Y) rewards" }] }
                : list).ToList(),
        };
        Assert.Same(readded, TodoMigrations.Apply(readded));
    }
}
