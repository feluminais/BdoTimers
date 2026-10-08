using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.Tests;

public class OverlayUpcomingPanelTests
{
    [Fact]
    public void A_scheduled_timer_gets_a_row_whose_choice_is_saved_in_the_overlay_settings() => WpfTest.Run(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Upcoming." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, root);
            var war = new TimerDef
            {
                Name = "Guild war", Kind = TimerKind.Scheduled,
                Scheduled = new ScheduledSpec { TimeZoneId = "UTC", Slots = [new Slot(DayOfWeek.Saturday, new TimeOnly(20, 0))] },
            };
            services.Timers.Upsert(war);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720 };
            try
            {
                window.Show();
                var settings = new SettingsPanelViewModel(services) { OpenAt = "Overlay" };
                main.OpenPanel(settings);
                WpfTest.Drain();
                var panel = settings.Overlay;
                Assert.True(panel.HasEvents);
                Assert.DoesNotContain(panel.Events, r => r.Name == "Farm");
                Assert.Single(panel.Events, r => r.Name == "Guild bosses");
                var row = panel.Events.Single(r => r.Name == "Guild war");
                Assert.False(row.IsOn);
                Assert.Empty(services.Settings.Current.Overlay.Timers);

                row.IsOn = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 60)], services.Settings.Current.Overlay.Timers);
                row.BeforeText = "3:00";
                Assert.Equal([new OverlayTimerWindow(war.Id, 180)], services.Settings.Current.Overlay.Timers);
                row.BeforeText = "soon";
                Assert.True(row.BeforeInvalid);
                Assert.Equal([new OverlayTimerWindow(war.Id, 180)], services.Settings.Current.Overlay.Timers);
                row.BeforeText = "45";
                Assert.False(row.BeforeInvalid);
                Assert.Equal([new OverlayTimerWindow(war.Id, 45)], services.Settings.Current.Overlay.Timers);
                row.Always = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 45, Always: true)], services.Settings.Current.Overlay.Timers);
                row.IsOn = false;
                Assert.Empty(services.Settings.Current.Overlay.Timers);

                row.IsOn = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 45, Always: true)], services.Settings.Current.Overlay.Timers);
                row.Always = false;
                row.BeforeText = "3:00";
                PanelFocusScope.Descendants(window).OfType<FrameworkElement>()
                    .First(e => System.Windows.Automation.AutomationProperties.GetName(e) == "Guild war").BringIntoView();
                WpfTest.Drain();
                UiCapture.Save(window, "overlay-upcoming.png");
                main.ClosePanel();
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });

    [Fact]
    public void The_overlay_draws_listed_rows_and_a_dimmed_sample_in_the_preview_when_none_is_inside_its_window() => WpfTest.Run(() =>
    {
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        var war = new TimerDef { Name = "Guild war", Kind = TimerKind.Scheduled };
        var settings = new OverlaySettings { Timers = [new OverlayTimerWindow(war.Id, 180)] };
        var model = new OverlayViewModel(new ArtLibrary(Path.GetTempPath()));

        model.Update(new OverlaySnapshot { Upcoming = [new(war, now.AddHours(2))] }, settings, now, preview: false);
        var row = Assert.Single(model.PopUps);
        Assert.Equal(("Guild war", "02:00:00", false), (row.Name, row.Time, row.IsSample));

        model.Update(new OverlaySnapshot(), settings, now, preview: false);
        Assert.Empty(model.PopUps);
        model.Update(new OverlaySnapshot(), settings, now, preview: true);
        Assert.True(Assert.Single(model.PopUps).IsSample);
        model.Update(new OverlaySnapshot(), new OverlaySettings(), now, preview: true);
        Assert.Empty(model.PopUps);
        // The Guild bosses pop-up is no row of its own in the preview.
        model.Update(new OverlaySnapshot(), new OverlaySettings { GuildBosses = new OverlayAlert { Enabled = true } }, now, preview: true);
        Assert.Empty(model.PopUps);
    });
}
