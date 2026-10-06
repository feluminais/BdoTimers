using System.IO;
using System.Windows;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Tests;

public class TodayViewModelTests
{
    sealed class Host : IPanelHost
    {
        public object? Opened;
        public void OpenPanel(object panel) => Opened = panel;
        public void ClosePanel() { }
        public bool IsOpen(object panel) => ReferenceEquals(Opened, panel);
    }

    static void WithServices(Action<AppServices> test) => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Today." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            test(services);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    static TimerDef[] Bosses(params string[] names) => names.Select(n => new TimerDef { Name = n, IsBuiltIn = true }).ToArray();

    [Fact]
    public void The_hero_shows_the_next_spawn_with_the_one_after_it_the_one_before_it_and_how_soon() => WithServices(services =>
    {
        var hero = new HeroViewModel(services, _ => { }, () => { });
        var now = new DateTimeOffset(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);
        var board = new BossBoardState(
            new SpawnGroup(now.AddHours(-8).AddSeconds(-2), Bosses("Golden Pig King", "Kutum"), false),
            new SpawnGroup(now.AddHours(2).AddMinutes(47).AddSeconds(10), Bosses("Muraka", "Quint"), false),
            new SpawnGroup(now.AddHours(3).AddMinutes(59).AddSeconds(58), Bosses("Garmoth"), false));

        hero.Update(board, now, [15, 5, 0]);

        Assert.True(hero.HasNext);
        Assert.Equal("02:47:10", hero.Next.Clock);
        Assert.StartsWith("Next · ", hero.Next.Label);
        Assert.Equal("Then Garmoth 03:59:58 · Alerts 15 · 5 · at spawn", hero.Then);
        Assert.Equal("Previous Golden Pig King · Kutum −08:00:02", hero.Previous);
        Assert.Equal(UrgencyLevel.Normal, hero.Level);

        hero.Update(board, now.AddHours(2).AddMinutes(40), [15, 5, 0]);
        Assert.Equal(UrgencyLevel.Imminent, hero.Level);
        hero.Update(new BossBoardState(null, null, null), now, [15, 5, 0]);
        Assert.False(hero.HasNext);
        Assert.Null(hero.Then);
        Assert.Null(hero.Previous);
    });

    [Fact]
    public void Skip_mutes_every_boss_of_the_spawn_at_once_and_Unskip_brings_them_back() => WithServices(services =>
    {
        var hero = new HeroViewModel(services, _ => { }, () => { });
        var now = new DateTimeOffset(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);
        var bosses = Bosses("Kzarka", "Nouver");
        var at = now.AddHours(1);
        hero.Update(new BossBoardState(null, new SpawnGroup(at, bosses, false), null), now, []);
        Assert.Equal("Skip", hero.SkipLabel);

        hero.ToggleSkipCommand.Execute(null);

        Assert.Equal(2, services.Timers.Current.Muted.Count);
        hero.Update(new BossBoardState(null, new SpawnGroup(at, bosses, true), null), now, []);
        Assert.Equal("Unskip", hero.SkipLabel);

        hero.ToggleSkipCommand.Execute(null);

        Assert.Empty(services.Timers.Current.Muted);
    });

    [Fact]
    public void Coming_up_lists_the_next_24_hours_and_leaves_out_bosses_with_alerts_off() => WithServices(services =>
    {
        var list = new ComingUpViewModel(services, new Host());
        var now = services.Clock.UtcNow;

        list.Refresh(now);

        Assert.False(list.IsEmpty);
        Assert.Contains(list.Rows, row => row.Kind == CalendarKind.Boss);
        Assert.Contains(list.Rows, row => row.IsNext);
        Assert.All(list.Rows, row => Assert.Matches("^in |^now$", row.Until));
        var last = list.Rows[^1];
        list.Refresh(now.AddSeconds(5));
        Assert.Contains(last, list.Rows);

        foreach (var boss in services.Timers.Current.Timers.Where(t => t.IsBuiltIn)) services.Timers.SetEnabled(boss.Id, false);
        list.Refresh(now.AddSeconds(6));

        Assert.DoesNotContain(list.Rows, row => row.Kind == CalendarKind.Boss);
    });

    [Fact]
    public void Bosses_that_spawn_together_share_one_row_with_a_button_per_name_and_a_skip_of_its_own() => WithServices(services =>
    {
        var host = new Host();
        var list = new ComingUpViewModel(services, host);
        var now = services.Clock.UtcNow;
        list.Refresh(now);

        var shared = list.Rows.First(row => row.Names.Count > 1);

        Assert.Equal(CalendarKind.Boss, shared.Kind);
        Assert.False(shared.Names[0].Follows);
        Assert.All(shared.Names.Skip(1), name => Assert.True(name.Follows));
        // No two bosses of one spawn are listed apart.
        var bossRows = list.Rows.Where(row => row.Kind == CalendarKind.Boss).ToList();
        Assert.Equal(bossRows.Count, bossRows.Select(row => row.Time).Distinct().Count());

        shared.Names[0].Row.OpenCommand.Execute(null);
        Assert.IsType<BossPanelViewModel>(host.Opened);

        var skipped = shared.Names[1];
        skipped.Row.ToggleSkipCommand.Execute(null);
        list.Refresh(now.AddSeconds(5));

        Assert.Single(services.Timers.Current.Muted);
        var again = list.Rows.Single(row => ReferenceEquals(row, shared));
        Assert.Equal(CellState.Skipped, again.Names[1].Row.State);
        Assert.NotEqual(CellState.Skipped, again.Names[0].Row.State);
        Assert.Equal(shared.Names.Count, again.Names.Count);
    });

    [Fact]
    public void Running_lists_started_and_paused_countdowns_with_their_progress() => WithServices(services =>
    {
        var host = new Host();
        var custom = new CustomViewModel(services, host);
        var today = new TodayViewModel(services, host, custom, new TodoViewModel(services, host), () => { }, () => { }, () => { });
        Assert.False(today.HasRunning);
        var farm = services.Timers.Current.Timers.First(t => t.Preset == Presets.Farm);

        services.Timers.Start(farm.Id, services.Clock.UtcNow.AddHours(-11));
        WpfTest.Drain();
        today.Refresh(services.Clock.UtcNow);

        Assert.True(today.HasRunning);
        var tile = Assert.Single(today.Running);
        Assert.Equal("Farm", tile.Name);
        Assert.InRange(tile.Progress!.Value, 0.45, 0.55);

        services.Timers.Pause(farm.Id, services.Clock.UtcNow);
        WpfTest.Drain();
        today.Refresh(services.Clock.UtcNow);
        Assert.Single(today.Running);

        services.Timers.Reset(farm.Id);
        WpfTest.Drain();
        today.Refresh(services.Clock.UtcNow);
        Assert.False(today.HasRunning);
    });

    [Fact]
    public void Daily_tasks_show_the_open_rows_of_the_active_daily_lists_and_count_the_progress() => WithServices(services =>
    {
        var daily = services.Todos.Current.Lists.First(l => l.Cadence == TodoCadence.Daily);
        var tasks = new TaskPanelViewModel(services, new TodoViewModel(services, new Host()), () => { }, TodoCadence.Daily);
        Assert.False(tasks.HasLists);
        Assert.Equal("To-do", tasks.Footer);
        Assert.Equal("Daily tasks", tasks.Heading);

        services.Todos.SetEnabled(daily.Id, true);
        var todo = new TodoViewModel(services, new Host());
        tasks = new TaskPanelViewModel(services, todo, () => { }, TodoCadence.Daily);

        Assert.True(tasks.HasLists);
        Assert.Equal(3, tasks.Tasks.Count);
        Assert.Equal("0/3", tasks.Summary);
        Assert.Null(tasks.Footer);

        services.Todos.Toggle(daily.Id, daily.Rows[0].Id);
        WpfTest.Drain();

        Assert.Equal(2, tasks.Tasks.Count);
        Assert.Equal("1/3", tasks.Summary);
        Assert.Equal(1.0 / 3, tasks.Fraction, 3);
    });

    [Fact]
    public void Weekly_tasks_show_the_open_top_level_tasks_with_how_far_along_each_is_and_count_what_does_not_fit() => WithServices(services =>
    {
        var weekly = services.Todos.Current.Lists.First(l => l.Cadence == TodoCadence.Weekly);
        var todo = new TodoViewModel(services, new Host());
        var panel = new TaskPanelViewModel(services, todo, () => { }, TodoCadence.Weekly);
        Assert.False(panel.HasLists);
        Assert.Equal("Weekly tasks", panel.Heading);

        services.Todos.SetEnabled(weekly.Id, true);
        WpfTest.Drain();

        Assert.True(panel.HasLists);
        Assert.Equal("0/16", panel.Summary);
        Assert.Equal(weekly.Rows.Select(row => row.Text), panel.Tasks.Select(task => task.Row.Text));
        Assert.Null(panel.Footer);

        // A task with sub-tasks stays on one line, saying how far along it is, until all of them are done.
        var first = weekly.Rows.First(row => row.Children.Count > 1);
        services.Todos.Toggle(weekly.Id, first.Children[0].Id);
        WpfTest.Drain();

        var line = Assert.Single(panel.Tasks, task => task.Row.Text == first.Text).Row;
        Assert.Equal($"1/{first.Children.Count}", line.PartialProgress);
        Assert.Equal("1/16", panel.Summary);
        foreach (var child in first.Children.Skip(1)) services.Todos.Toggle(weekly.Id, child.Id);
        WpfTest.Drain();

        Assert.DoesNotContain(panel.Tasks, task => task.Row.Text == first.Text);

        // A task that is opened stays the same line while its sub-tasks are ticked, so it stays open.
        var second = weekly.Rows.First(row => row.Children.Count > 1 && row.Id != first.Id);
        var opened = panel.Tasks.Single(task => task.Row.Text == second.Text);
        opened.ToggleExpandedCommand.Execute(null);
        services.Todos.Toggle(weekly.Id, second.Children[0].Id);
        WpfTest.Drain();

        Assert.Same(opened, Assert.Single(panel.Tasks, task => task.Row.Text == second.Text));
        Assert.True(opened.IsExpanded);
        Assert.True(opened.HasChildren);

        // Eight tasks of one line each: six show and the rest are counted.
        services.Todos.ReplaceRows(weekly.Id, Enumerable.Range(1, 8).Select(n => new TodoRow { Text = $"Task {n}" }).ToList());
        WpfTest.Drain();

        Assert.Equal(6, panel.Tasks.Count);
        Assert.Equal("+2 more", panel.Footer);
        Assert.Equal("0/8", panel.Summary);
        panel.ShowTodoCommand.Execute(null);
    });
}
