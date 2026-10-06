using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;

namespace BdoTimers.App.Tests;

/// <summary>Horse registrations are counted on the one Horse registration card instead of each being a timer of its own.</summary>
public class HorseCardTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    sealed class Host : IPanelHost
    {
        public object? Opened;
        public void OpenPanel(object panel) => Opened = panel;
        public void ClosePanel() { }
        public bool IsOpen(object panel) => ReferenceEquals(Opened, panel);
    }

    static void WithServices(Action<AppServices> test) => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Horse." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            test(services);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    /// <summary>Starts three registrations a minute apart, the first at <paramref name="first"/>.</summary>
    static void StartThree(AppServices services, DateTimeOffset? first = null)
    {
        for (var i = 0; i < 3; i++)
            Assert.Equal(HorseStartResult.Started, services.Timers.StartHorseRegistration((first ?? T0).AddMinutes(i)));
        WpfTest.Drain();
    }

    static List<TimerDef> Runs(AppServices services) => services.Timers.Current.Timers.Where(Presets.IsActiveHorseRun).ToList();

    [Fact]
    public void Registrations_under_way_get_no_tiles_and_the_card_counts_them_with_the_soonest_clock() => WithServices(services =>
    {
        var timers = new CustomViewModel(services, new Host());
        var horse = timers.Items.Single(t => t.IsHorseTemplate);
        Assert.False(horse.CanStop);
        var rested = horse.Digits;

        StartThree(services);
        var now = T0.AddMinutes(2);
        timers.Refresh(now);

        var runs = Runs(services);
        Assert.Equal(3, runs.Count);
        Assert.DoesNotContain(timers.Items, tile => runs.Any(run => run.Id == tile.Id));
        Assert.True(horse.CanStop);
        Assert.True(horse.HasHorseRuns);
        Assert.Equal("3 of 10 active", horse.Detail);
        Assert.Equal(DurationFormat.Clock(runs.Min(run => run.Countdown!.EndsAtUtc!.Value) - now), horse.Digits);
        Assert.NotEqual(rested, horse.Digits);
        Assert.Equal("Stop the latest", horse.StopTip);
    });

    [Fact]
    public void Stop_on_the_card_ends_the_latest_registration_and_undo_brings_it_back() => WithServices(services =>
    {
        var timers = new CustomViewModel(services, new Host());
        var horse = timers.Items.Single(t => t.IsHorseTemplate);
        StartThree(services);
        var latest = Runs(services).MaxBy(run => run.Countdown!.StartedAtUtc)!;

        horse.ResetCommand.Execute(null);
        WpfTest.Drain();

        Assert.Equal(2, Runs(services).Count);
        Assert.DoesNotContain(Runs(services), run => run.Id == latest.Id);
        services.Undo.UndoCommand.Execute(null);
        Assert.Contains(Runs(services), run => run.Id == latest.Id);
        Assert.Equal(3, Runs(services).Count);
    });

    [Fact]
    public void Today_lists_the_card_as_running_while_a_registration_is_under_way_and_not_the_runs() => WithServices(services =>
    {
        var host = new Host();
        var custom = new CustomViewModel(services, host);
        var today = new TodayViewModel(services, host, custom, new TodoViewModel(services, host), () => { }, () => { }, () => { });
        Assert.DoesNotContain(today.Running, tile => tile.IsHorseTemplate);

        StartThree(services);
        today.Refresh(T0.AddMinutes(2));

        var horse = Assert.Single(today.Running, tile => tile.IsHorseTemplate);
        Assert.Equal(custom.Items.Single(t => t.IsHorseTemplate), horse);
        Assert.DoesNotContain(today.Running, tile => tile.Name.StartsWith("Horse registration ", StringComparison.Ordinal));
    });

    [Fact]
    public void The_panel_lists_each_registration_to_stop_on_its_own_and_follows_new_ones() => WithServices(services =>
    {
        var host = new Host();
        var template = services.Timers.Current.Timers.Single(t => t.Preset == Presets.HorseRegistration);
        var panel = new CustomPanelViewModel(services, host, template);
        try
        {
            Assert.Empty(panel.Runs);
            Assert.False(panel.HasRuns);

            // The panel's clocks run on the real time.
            StartThree(services, services.Clock.UtcNow);

            Assert.True(panel.HasRuns);
            Assert.Equal(["Horse 1", "Horse 2", "Horse 3"], panel.Runs.Select(row => row.Name));
            Assert.Equal(3, panel.Runs.Select(row => row.Clock).Distinct().Count());

            panel.Runs[1].StopCommand.Execute(null);
            WpfTest.Drain();

            Assert.Equal(["Horse 1", "Horse 3"], panel.Runs.Select(row => row.Name));
            Assert.Equal(2, Runs(services).Count);
            services.Undo.UndoCommand.Execute(null);
            WpfTest.Drain();
            Assert.Equal(["Horse 1", "Horse 2", "Horse 3"], panel.Runs.Select(row => row.Name));
        }
        finally { panel.OnClosed(); }
    });

    [Fact]
    public void The_card_adds_a_registration_with_a_plus_and_the_runs_are_not_drawn_as_cards() => WithServices(services =>
    {
        var host = new Host();
        var timers = new CustomViewModel(services, host);
        StartThree(services);
        timers.Refresh(T0.AddMinutes(2));
        var view = new CustomView { DataContext = timers };
        var window = new Window { Content = view, Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            WpfTest.Drain();
            var horse = timers.Items.Single(t => t.IsHorseTemplate);
            var add = VisualTree.FindDescendant<Button>(view, b => b.Command == horse.StartHorseRegistrationCommand)!;
            Assert.True(add.IsVisible);
            Assert.IsType<PlusIcon>(add.Content);
            var cards = PanelFocusScope.Descendants(view).OfType<Button>().Count(b => timers.Items.Any(t => b.Command == t.OpenCommand));
            Assert.Equal(timers.Items.Count, cards);
            var stop = VisualTree.FindDescendant<Button>(view, b => b.Command == horse.ResetCommand)!;
            Assert.True(stop.IsVisible);
            Assert.Equal("Stop the latest", stop.ToolTip);
        }
        finally { window.Close(); }
    });
}
