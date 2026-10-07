using System.IO;
using System.Windows;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Tests;

public class StopwatchCardTests
{
    sealed class Host : IPanelHost
    {
        public object? Opened;
        public void OpenPanel(object panel) => Opened = panel;
        public void ClosePanel() { }
        public bool IsOpen(object panel) => ReferenceEquals(Opened, panel);
    }

    [Fact]
    public void A_new_tracker_can_be_a_stopwatch_with_no_alerts_that_starts_pauses_and_resets_on_its_card() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Stopwatch." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var host = new Host();
            new NewTimerPanelViewModel(services, host).StopwatchCommand.Execute(null);

            var panel = Assert.IsType<CustomPanelViewModel>(host.Opened);
            Assert.True(panel.IsStopwatch);
            Assert.False(panel.HasAlerts);
            panel.OnClosed();
            var saved = services.Timers.Current.Timers.Single(t => t.Kind == TimerKind.Stopwatch && t.Preset is null);
            Assert.Equal("New stopwatch", saved.Name);

            var timers = new CustomViewModel(services, host);
            var card = timers.Items.Single(t => t.Id == saved.Id);
            void Press(Action<TimerTileViewModel> action)
            {
                action(card);
                WpfTest.Drain();
                timers.Refresh(services.Clock.UtcNow);
            }

            Assert.True(card.HasControls);
            Assert.False(card.CanStop);
            Assert.Equal("Start", card.PlayPauseTip);
            Press(c => c.StartPauseCommand.Execute(null));
            Assert.True(card.IsRunning);
            Assert.True(card.CanStop);
            Assert.Equal("Pause", card.PlayPauseTip);
            Press(c => c.StartPauseCommand.Execute(null));
            Assert.True(card.IsPaused);
            Assert.Equal("Resume", card.PlayPauseTip);
            Press(c => c.ResetCommand.Execute(null));
            Assert.False(card.CanStop);
            Assert.Equal("Start", card.PlayPauseTip);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
