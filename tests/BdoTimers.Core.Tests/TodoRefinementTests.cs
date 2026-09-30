using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public sealed class TodoRefinementTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_list_of_a_cadence_follows_its_shared_reset()
    {
        using var dir = new TempDir();
        var settings = new AppSettings();
        var seed = TodoSeed.Create(Now, settings);
        var store = new TodoStore(new JsonFileStore<TodoData>(dir.File("todos.json"), () => seed), seed, new FakeClock(Now));
        var customId = store.CreateList(TodoCadence.Weekly, settings);
        var weeklyId = TodoSeed.WeeklyId;
        var child = seed.Lists[0].Rows[0] with { Done = true };
        store.Modify(weeklyId, l => l with { Rows = [child, .. l.Rows.Skip(1)] });
        var changed = settings with { WeeklyTodoReset = settings.WeeklyTodoReset with { Day = DayOfWeek.Friday, Hour = 12 } };

        store.ApplyDefaultSchedules(changed);

        var weekly = store.Current.Lists.Where(l => l.Cadence == TodoCadence.Weekly).ToList();
        Assert.Equal(2, weekly.Count);
        Assert.All(weekly, l => Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), l.NextResetUtc));
        Assert.True(weekly.Single(l => l.Id == weeklyId).Rows[0].Done);
        Assert.Contains(weekly, l => l.Id == customId);
    }

    [Fact]
    public void Reset_uses_shared_dst_rule_and_recovers_invalid_weekday()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var spring = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
        var schedule = TodoSchedule.DailyDefault with { Hour = 2, Minute = 30, LocalTime = true };
        Assert.Equal(ScheduleMath.LocalToUtc(new DateTime(2026, 3, 29, 2, 30, 0), berlin),
            TodoReset.Next(schedule, spring, berlin));

        var invalid = TodoSchedule.WeeklyDefault with { Day = (DayOfWeek)99 };
        Assert.Equal(TodoReset.Next(TodoSchedule.WeeklyDefault, Now), TodoReset.Next(invalid, Now));
        var invalidTime = TodoSchedule.DailyDefault with { Hour = 99, Minute = -1 };
        Assert.Equal(TodoReset.Next(TodoSchedule.DailyDefault, Now), TodoReset.Next(invalidTime, Now));
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
        var first = TodoOutline.InsertAfter(rows, 0);
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

    [Fact]
    public void Store_save_failure_keeps_memory_and_file_unchanged()
    {
        using var dir = new TempDir();
        var seed = TodoSeed.Create(Now, new AppSettings());
        var file = new JsonFileStore<TodoData>(dir.File("todos.json"), () => seed);
        var store = new TodoStore(file, seed, new FakeClock(Now));
        store.SetEnabled(TodoSeed.DailyId, true);
        var before = store.Current;
        Directory.CreateDirectory(dir.File("todos.json.tmp"));

        Assert.Throws<StateSaveException>(() => store.Toggle(TodoSeed.DailyId, before.Lists[1].Rows[0].Id));
        Assert.Same(before, store.Current);
        Assert.False(file.Load().Value.Lists[1].Rows[0].Done);
    }

    [Fact]
    public void Lists_checks_children_and_shared_schedule_survive_reload()
    {
        using var dir = new TempDir();
        var settings = new AppSettings();
        var seed = TodoSeed.Create(Now, settings);
        var file = new JsonFileStore<TodoData>(dir.File("todos.json"), () => seed);
        var store = new TodoStore(file, seed, new FakeClock(Now));
        var id = store.CreateList(TodoCadence.Weekly, settings);
        store.ReplaceRows(id, [new TodoRow { Text = "Parent", Children = [new TodoRow { Text = "Child" }] }]);
        store.Toggle(id, store.Current.Lists.Single(l => l.Id == id).Rows[0].Children[0].Id);

        var reloaded = new TodoStore(file, file.Load().Value, new FakeClock(Now));
        var list = reloaded.Current.Lists.Single(l => l.Id == id);
        Assert.True(list.Rows[0].Children[0].Done);
        Assert.Equal(TodoCheck.Done, TodoOps.Check(list.Rows[0]));
        Assert.Equal(TodoSchedule.WeeklyDefault, list.Schedule);
    }

    [Fact]
    public void Restore_recreates_a_missing_default_off_and_leaves_custom_lists()
    {
        using var dir = new TempDir();
        var seed = TodoSeed.Create(Now, new AppSettings());
        var data = seed with { Lists = [seed.Lists[0]] };
        var store = new TodoStore(new JsonFileStore<TodoData>(dir.File("todos.json"), () => data), data, new FakeClock(Now));
        var id = store.CreateList(TodoCadence.Weekly, new AppSettings());
        store.RestoreDefaults(new AppSettings());

        Assert.Contains(store.Current.Lists, l => l.Id == TodoSeed.DailyId && !l.Enabled && !l.Deleted);
        Assert.Contains(store.Current.Lists, l => l.Id == id);
        Assert.Equal([TodoSeed.WeeklyId, TodoSeed.DailyId], store.Current.Lists.Where(l => l.IsBuiltIn).Select(l => l.Id));
    }

    [Fact]
    public void New_lists_are_on_and_empty_and_seed_labels_match_spec()
    {
        var data = TodoSeed.Create(Now, new AppSettings());
        Assert.Equal("Weekly quests", data.Lists[0].Name);
        Assert.Equal("Daily tasks", data.Lists[1].Name);
        Assert.Equal([
            "Liana / Ludowig daily life skill quest",
            "Imperial Delivery",
            "Pit of the Undying",
        ], data.Lists[1].Rows.Select(row => row.Text));
        Assert.Equal(4, data.Lists[0].Rows.Single(r => r.Text == "Olvia Academy").Children.Count);
        using var dir = new TempDir();
        var store = new TodoStore(new JsonFileStore<TodoData>(dir.File("todos.json"), () => data), data, new FakeClock(Now));
        var id = store.CreateList(TodoCadence.Daily, new AppSettings());
        var list = store.Current.Lists.Single(l => l.Id == id);
        Assert.Equal("New list", list.Name);
        Assert.True(list.Enabled);
        Assert.Empty(list.Rows);
    }

    [Fact]
    public void Old_daily_defaults_update_once_without_losing_custom_rows_or_checks()
    {
        using var dir = new TempDir();
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
        var oldData = seed with { DefaultsVersion = 0, Lists = [seed.Lists[0], oldDaily, otherList] };
        var file = new JsonFileStore<TodoData>(dir.File("todos.json"), () => oldData);
        var store = new TodoStore(file, oldData, new FakeClock(Now));

        store.Update(TodoMigrations.Apply);

        var migrated = file.Load().Value;
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
