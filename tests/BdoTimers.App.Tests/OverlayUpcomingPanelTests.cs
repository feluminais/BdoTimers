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
                var panel = new OverlayPanelViewModel(services, main);
                main.OpenPanel(panel);
                WpfTest.Drain();
                Assert.True(panel.HasUpcoming);
                Assert.DoesNotContain(panel.Upcoming, r => r.Name == "Farm");
                var row = panel.Upcoming.Single(r => r.Name == "Guild war");
                Assert.Equal(["Off", "1 h before", "2 h before", "3 h before", "6 h before", "12 h before", "24 h before"],
                    row.Choices.Select(c => c.Label));

                row.Lead = row.Choices.Single(c => c.Label == "3 h before");
                Assert.Equal([new OverlayTimerWindow(war.Id, 180)], services.Settings.Current.Overlay.Timers);
                row.Lead = row.Choices[0];
                Assert.Empty(services.Settings.Current.Overlay.Timers);

                row.Lead = row.Choices.Single(c => c.Label == "3 h before");
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
    });
}
