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
                Assert.Equal(["Next boss", "Farm", "Horse registrations", "Custom timers"], panel.Timers.Take(4).Select(r => r.Name));
                Assert.Single(panel.Timers, r => r.Name == "Guild bosses");
                Assert.DoesNotContain(panel.Timers, r => r.Name == "Fishing");
                var row = panel.Timers.Single(r => r.Name == "Guild war");
                Assert.False(row.IsOn);
                Assert.Empty(services.Settings.Current.Overlay.Timers);

                row.IsOn = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 60)], services.Settings.Current.Overlay.Timers);
                Assert.Equal(("1", "h"), (row.Amount, row.Unit.Label));
                row.Amount = "3";
                Assert.Equal([new OverlayTimerWindow(war.Id, 180)], services.Settings.Current.Overlay.Timers);
                row.Amount = "soon";
                Assert.True(row.AmountInvalid);
                Assert.Equal([new OverlayTimerWindow(war.Id, 180)], services.Settings.Current.Overlay.Timers);
                row.Amount = "999";
                Assert.True(row.AmountInvalid);
                row.Unit = row.Units[0];
                row.Amount = "45";
                Assert.False(row.AmountInvalid);
                Assert.Equal([new OverlayTimerWindow(war.Id, 45)], services.Settings.Current.Overlay.Timers);
                row.Always = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 45, Always: true)], services.Settings.Current.Overlay.Timers);
                row.IsOn = false;
                Assert.Empty(services.Settings.Current.Overlay.Timers);

                row.IsOn = true;
                Assert.Equal([new OverlayTimerWindow(war.Id, 45, Always: true)], services.Settings.Current.Overlay.Timers);
                row.Always = false;
                row.Unit = row.Units[1];
                row.Amount = "3";

                // The built-in rows keep their section's switch and save their lead beside it.
                var farm = panel.Timers.Single(r => r.Name == "Farm");
                Assert.True(farm.IsOn);
                Assert.True(farm.Always);
                farm.Always = false;
                Assert.Equal((true, new OverlayLead(false, 60)), (services.Settings.Current.Overlay.ShowFarm, services.Settings.Current.Overlay.FarmLead));
                farm.Unit = farm.Units[1];
                farm.Amount = "2";
                Assert.Equal(new OverlayLead(false, 120), services.Settings.Current.Overlay.FarmLead);
                farm.IsOn = false;
                Assert.False(services.Settings.Current.Overlay.ShowFarm);
                Assert.Equal(new OverlayLead(false, 120), services.Settings.Current.Overlay.FarmLead);
                var custom = panel.Timers.Single(r => r.Name == "Custom timers");
                custom.Always = false;
                custom.Unit = custom.Units[0];
                custom.Amount = "10";
                Assert.Equal(new OverlayLead(false, 10), services.Settings.Current.Overlay.CustomLead);
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
