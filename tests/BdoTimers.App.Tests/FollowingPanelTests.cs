using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class FollowingPanelTests
{
    sealed class Host : IPanelHost
    {
        public object? Opened;
        public void OpenPanel(object panel) => Opened = panel;
        public void ClosePanel() { }
        public bool IsOpen(object panel) => ReferenceEquals(Opened, panel);
    }

    [Fact]
    public void Lists_the_regions_bosses_and_a_switch_turns_a_boss_off_and_on() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Follow." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            WpfTest.WithoutVoice(services);
            var panel = new FollowingPanelViewModel(services, new Host());
            Assert.NotEmpty(panel.Bosses);
            Assert.Equal($"{panel.Bosses.Count} of {panel.Bosses.Count}", panel.Summary);
            var row = panel.Bosses[0];
            Assert.True(row.IsOn);
            row.IsOn = false;
            WpfTest.Drain();
            Assert.False(services.Timers.Current.Timers.Single(t => t.Id == row.Id).Enabled);
            Assert.Equal("Alerts off", row.Detail);
            Assert.Equal($"{panel.Bosses.Count - 1} of {panel.Bosses.Count}", panel.Summary);
            row.IsOn = true;
            WpfTest.Drain();
            Assert.True(services.Timers.Current.Timers.Single(t => t.Id == row.Id).Enabled);
            Assert.NotEqual("Alerts off", row.Detail);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void The_Schedule_header_opens_the_list_from_a_named_button() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Follow." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var host = new Host();
            var schedule = new ScheduleViewModel(new BossesViewModel(services, host), new CalendarViewModel(services, host),
                () => host.OpenPanel(new FollowingPanelViewModel(services, host)));
            var view = new ScheduleView { DataContext = schedule };
            var window = new Window { Content = view, Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                var button = VisualTree.FindDescendant<Button>(view, b => b.Command == schedule.OpenFollowingCommand)!;
                Assert.Equal("Following", new ButtonAutomationPeer(button).GetName());
                button.Command!.Execute(null);
                Assert.IsType<FollowingPanelViewModel>(host.Opened);
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void A_name_opens_that_boss_and_Add_boss_opens_a_new_one() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Follow." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var host = new Host();
            var panel = new FollowingPanelViewModel(services, host);
            panel.Bosses[0].OpenCommand.Execute(null);
            Assert.IsType<BossPanelViewModel>(host.Opened);
            var before = services.Timers.Current.Timers.Count;
            panel.AddBossCommand.Execute(null);
            Assert.Equal(before + 1, services.Timers.Current.Timers.Count);
            Assert.IsType<BossPanelViewModel>(host.Opened);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
