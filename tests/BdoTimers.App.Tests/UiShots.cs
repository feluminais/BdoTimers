using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Tests;

/// <summary>Opt-in: with BDOTIMERS_SHOTS set to a folder, saves a PNG of each screen and the main panels at 960 x 720.</summary>
public class UiShots
{
    [Fact]
    public void Screens_and_panels_render() => WpfTest.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("BDOTIMERS_SHOTS") is null) return;
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Shots." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            var farm = services.Timers.Current.Timers.First(t => t.Preset == Presets.Farm);
            services.Timers.Start(farm.Id, services.Clock.UtcNow.AddHours(-12));
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services)
            { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                // The lists start off; look at that, then turn them on and tick some rows for the other screens.
                ((RadioButton)window.FindName("TodoTab")).IsChecked = true;
                WpfTest.Drain();
                UiCapture.Save(window, "todo-off.png");
                foreach (var list in services.Todos.Current.Lists) services.Todos.SetEnabled(list.Id, true);
                var weekly = services.Todos.Current.Lists.First(l => l.Cadence == TodoCadence.Weekly);
                var daily = services.Todos.Current.Lists.First(l => l.Cadence == TodoCadence.Daily);
                services.Todos.Toggle(weekly.Id, weekly.Rows[0].Id);
                services.Todos.Toggle(weekly.Id, weekly.Rows[1].Children[0].Id);
                services.Todos.Toggle(daily.Id, daily.Rows[0].Id);
                // The Garmoth tracker is off until Settings turns it on; the README shows it with one kill marked.
                services.Settings.Update(s => s with { GarmothTracker = true });
                services.Timers.MarkGarmoth(1, services.Clock.UtcNow);
                WpfTest.Drain();
                foreach (var (tab, name) in new[] { ("TodayTab", "today"), ("ScheduleTab", "schedule"), ("CustomTab", "timers"), ("TodoTab", "todo") })
                {
                    // Today shows Farm running; the Timers screen has every kind of timer going.
                    if (name == "timers") StartTheOtherTimers(services);
                    ((RadioButton)window.FindName(tab)).IsChecked = true;
                    WpfTest.Drain();
                    UiCapture.Save(window, $"{name}.png");
                    if (name == "timers")
                    {
                        // A third registration turns the Horse registration card's last line into an ellipsis.
                        services.Timers.StartHorseRegistration(services.Clock.UtcNow.AddMinutes(-2));
                        WpfTest.Drain();
                        UiCapture.Save(window, "timers-horse-3.png");
                    }
                    if (name == "schedule")
                    {
                        var month = PanelFocusScope.Descendants(window).OfType<RadioButton>().First(b => b.Name == "MonthToggle");
                        month.IsChecked = true;
                        WpfTest.Drain();
                        UiCapture.Save(window, "schedule-month.png");
                        var busy = AddBusyDay(services);
                        UiCapture.Save(window, "schedule-month-busy.png");
                        // The events would show on Timers, which is captured next.
                        foreach (var id in busy) services.Timers.Delete(id);
                        WpfTest.Drain();
                        PanelFocusScope.Descendants(window).OfType<RadioButton>().First(b => b.Name == "WeekToggle").IsChecked = true;
                        WpfTest.Drain();
                    }
                }
                main.OpenSettingsCommand.Execute(null);
                WpfTest.Drain();
                var settings = PanelFocusScope.Descendants(window).OfType<SettingsPanel>().Single();
                foreach (var (nav, name) in new[] { ("GeneralNav", "general"), ("AlertsNav", "alerts"), ("TodoNav", "todo"), ("BossesNav", "bosses"), ("DataNav", "data"), ("AboutNav", "about") })
                {
                    ((RadioButton)settings.FindName(nav)).IsChecked = true;
                    WpfTest.Drain();
                    UiCapture.Save(window, $"settings-{name}.png");
                }
                main.ClosePanel();
                WpfTest.Drain();
                var data = services.Timers.Current;
                var boss = data.Timers.First(t => BossRegions.IsSelected(data, t));
                main.OpenPanel(new BossPanelViewModel(services, main, boss));
                WpfTest.Drain();
                UiCapture.Save(window, "boss-panel.png");
                main.ClosePanel();
                WpfTest.Drain();
                main.OpenPanel(new FollowingPanelViewModel(services, main));
                WpfTest.Drain();
                UiCapture.Save(window, "following.png");
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });

    /// <summary>A day a week from now with three events of its own: as many lines as a month cell has room for, besides the bosses.</summary>
    static List<Guid> AddBusyDay(AppServices services)
    {
        var day = DateOnly.FromDateTime(services.Clock.UtcNow.UtcDateTime).AddDays(7);
        var events = new[] { "Node war", "Siege", "Guild meeting" }
            .Select(title => new TimerDef { Name = title, Kind = TimerKind.OneTime, OneTime = new OneTimeSpec { Date = day, Time = new TimeOnly(20, 0) } }).ToList();
        foreach (var added in events) services.Timers.Upsert(added);
        WpfTest.Drain();
        return events.Select(added => added.Id).ToList();
    }

    static void StartTheOtherTimers(AppServices services)
    {
        var now = services.Clock.UtcNow;
        var timers = services.Timers;
        timers.Start(timers.Current.Timers.First(t => t.Preset == Presets.Fishing).Id, now.AddMinutes(-138));
        timers.Modify(timers.Current.Timers.First(t => t.Preset == Presets.GuildBosses).Id,
            t => t with { Scheduled = t.Scheduled! with { Off = false } });
        var buffs = new TimerDef { Name = "Grinding buffs", Kind = TimerKind.Countdown, Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(45) } };
        timers.Upsert(buffs);
        timers.Start(buffs.Id, now.AddMinutes(-3));
        // A stopwatch of the player's own, like Fishing.
        var session = new TimerDef { Name = "Hunting session", Kind = TimerKind.Stopwatch, Stopwatch = new StopwatchSpec() };
        timers.Upsert(session);
        timers.Start(session.Id, now.AddMinutes(-52));
        // The second Horse registration gets a clock of its own under the first.
        timers.StartHorseRegistration(now.AddMinutes(-4));
        timers.StartHorseRegistration(now.AddMinutes(-1));
        WpfTest.Drain();
    }

    /// <summary>The smallest supported cases: 640 wide at 100 %, and the minimum window at 150 % text.</summary>
    [Fact]
    public void Narrow_windows_and_large_text_render() => WpfTest.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("BDOTIMERS_SHOTS") is null) return;
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Shots." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            var farm = services.Timers.Current.Timers.First(t => t.Preset == Presets.Farm);
            services.Timers.Start(farm.Id, services.Clock.UtcNow.AddHours(-12));
            foreach (var list in services.Todos.Current.Lists) services.Todos.SetEnabled(list.Id, true);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 640, Height = 540, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                var tabs = new[] { ("TodayTab", "today"), ("ScheduleTab", "schedule"), ("CustomTab", "timers"), ("TodoTab", "todo") };
                AddBusyDay(services);
                void Capture(string suffix)
                {
                    foreach (var (tab, name) in tabs)
                    {
                        ((RadioButton)window.FindName(tab)).IsChecked = true;
                        WpfTest.Drain();
                        UiCapture.Save(window, $"{name}-{suffix}.png");
                        if (name != "schedule") continue;
                        var toggles = PanelFocusScope.Descendants(window).OfType<RadioButton>().ToList();
                        toggles.First(b => b.Name == "MonthToggle").IsChecked = true;
                        WpfTest.Drain();
                        UiCapture.Save(window, $"schedule-month-{suffix}.png");
                        toggles.First(b => b.Name == "WeekToggle").IsChecked = true;
                        WpfTest.Drain();
                    }
                }
                Capture("640");
                services.Settings.Update(s => s with { TextScale = 1.5 });
                WpfTest.Drain();
                window.Width = 960;
                window.Height = 720;
                WpfTest.Drain();
                Capture("150-min");
                window.Width = 1440;
                window.Height = 1080;
                WpfTest.Drain();
                Capture("150");
                main.OpenPanel(new BossPanelViewModel(services, main, services.Timers.Current.Timers.First(t => t.IsBuiltIn)));
                WpfTest.Drain();
                UiCapture.Save(window, "boss-panel-150.png");
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });
}
