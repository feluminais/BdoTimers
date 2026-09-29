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
    public void New_lists_are_on_and_empty_and_seed_labels_match_spec()
    {
        var data = TodoSeed.Create(Now, new AppSettings());
        Assert.Equal("Weekly quests", data.Lists[0].Name);
        Assert.Equal("Daily tasks", data.Lists[1].Name);
        Assert.Equal(4, data.Lists[0].Rows.Single(r => r.Text == "Olvia Academy").Children.Count);
        using var dir = new TempDir();
        var store = new TodoStore(new JsonFileStore<TodoData>(dir.File("todos.json"), () => data), data, new FakeClock(Now));
        var id = store.CreateList(TodoCadence.Daily, new AppSettings());
        var list = store.Current.Lists.Single(l => l.Id == id);
        Assert.Equal("New list", list.Name);
        Assert.True(list.Enabled);
        Assert.Empty(list.Rows);
    }
}
