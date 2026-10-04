using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.App.Art;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Tests;

public sealed class RepeatTimerTests
{
    [Fact]
    public void Calendar_uses_the_label_for_the_selected_occurrence()
    {
        var timer = Presets.Create().Single(t => t.Preset == Presets.WarOfTheRoses);
        var item = new CalendarItem(CalendarKind.Weekly, new(2026, 10, 4, 15, 0, 0, TimeSpan.Zero), timer, CellState.Upcoming);
        Assert.Equal("War of the Roses · Battle", new CalendarRowViewModel(item, _ => { }, (_, _) => { }).Name);
    }

    [Fact]
    public void Panel_persists_repeat_validates_all_fields_and_reload_restores_region_defaults() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Roses." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var timer = services.Timers.Current.Timers.Single(t => t.Preset == Presets.WarOfTheRoses);
            var panel = new CustomPanelViewModel(services, new PanelHost(), timer);
            var view = new CustomPanel { DataContext = panel };
            var window = new Window { Content = view, Width = 460, Height = 900, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                var repeat = PanelFocusScope.Descendants(view).OfType<TextBox>()
                    .Single(b => new TextBoxAutomationPeer(b).GetName() == "Repeat every weeks");
                repeat.Text = "3";
                repeat.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("3", panel.EveryWeeksText);
                Assert.True(panel.Slots!.HasLabels);
                panel.EveryWeeksText = "3";
                panel.WeekAnchorText = "2026-10-04";
                var saved = services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!;
                Assert.Equal(3, saved.EveryWeeks);
                Assert.Equal(new DateOnly(2026, 10, 4), saved.WeekAnchor);
                panel.EveryWeeksText = "53";
                Assert.False(panel.ScheduleValid);
                panel.StartDateText = "2026-10-01";
                Assert.False(panel.ScheduleValid);
                panel.EveryWeeksText = "3";
                panel.WeekAnchorText = "bad";
                Assert.False(panel.ScheduleValid);
                panel.EveryWeeksText = "4";
                Assert.False(panel.ScheduleValid);
                panel.WeekAnchorText = "2026-10-04";
                panel.EndDateText = "2026-09-01";
                Assert.False(panel.ScheduleValid);
                panel.WeekAnchorText = "2026-10-18";
                Assert.False(panel.ScheduleValid);
                panel.ResetTimes.AskCommand.Execute(null);
                panel.ResetTimes.CancelCommand.Execute(null);
                Assert.Equal(4, services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.EveryWeeks);
                services.SelectBossRegion(BossRegions.NorthAmerica);
                Assert.Contains("NA", panel.ResetTimesLabel);
                panel.ResetTimes.AskCommand.Execute(null);
                panel.ResetTimes.ConfirmCommand.Execute(null);
                Assert.True(panel.ScheduleValid);
                Assert.Equal("2", panel.EveryWeeksText);
                Assert.Equal("2026-09-20", panel.WeekAnchorText);
                Assert.Equal("", panel.StartDateText);
                Assert.Equal("", panel.EndDateText);
                Assert.Equal(new[] { "13:05", "15:00" }, panel.Slots.Rows.Select(r => r.TimeText));
                Assert.Equal("Battle", panel.Slots.Rows[1].LabelText);
                WpfTest.Drain();
                Assert.Equal("2", repeat.Text);
                Assert.Contains(PanelFocusScope.Descendants(view).OfType<TextBox>(), b => b.Text == "13:05");
                Assert.Contains(PanelFocusScope.Descendants(view).OfType<TextBox>(), b => b.Text == "Battle" && b.IsVisible);
                if (Environment.GetEnvironmentVariable("BDOTIMERS_TEST_CAPTURE") is { Length: > 0 } capture)
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight,
                        96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(view);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = File.Create(capture);
                    encoder.Save(stream);
                }
                var finish = PanelFocusScope.Descendants(view).OfType<Button>().Single(b => Equals(b.Content, "Done") && b.DataContext == panel);
                Assert.True(finish.IsEnabled);
                panel.Slots.Rows[1].TimeText = "bad";
                WpfTest.Drain();
                Assert.False(finish.IsEnabled);
                panel.Slots.Rows[1].TimeText = "16:00";
                WpfTest.Drain();
                Assert.True(finish.IsEnabled);
                Assert.Equal(new TimeOnly(16, 0), services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.Slots[1].Time);
            }
            finally { window.Close(); panel.OnClosed(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void Removing_an_event_boss_flushes_its_pending_name_before_undo() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.BossEdits." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var boss = services.Timers.AddBoss();
            var name = new TextBox();
            var panel = new BossPanelViewModel(services, new PanelHost(() => PanelEdits.Complete(name)), boss);
            name.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(panel.Name))
            {
                Source = panel, Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged, Delay = 300,
            });
            name.Text = "Event boss";
            Assert.Equal(boss.Name, panel.Name);
            panel.Remove.AskCommand.Execute(null);
            panel.Remove.ConfirmCommand.Execute(null);
            services.Undo.UndoCommand.Execute(null);
            Assert.Equal("Event boss", services.Timers.Current.Timers.Single(t => t.Id == boss.Id).Name);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void Boss_grid_refreshes_when_an_event_boss_date_limit_changes() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.BossDates." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var boss = services.Timers.AddBoss();
            services.Timers.Modify(boss.Id, t => t with
            {
                Scheduled = new ScheduledSpec { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Thursday, new(20, 0))] },
            });
            var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
            var board = new BossesViewModel(services, new PanelHost());
            board.Refresh(now);
            Assert.Contains(board.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Entries), e => e.Name == boss.Name);
            services.Timers.SetWeeklyDateRange(boss.Id, null, new DateOnly(2026, 10, 7));
            board.Refresh(now);
            Assert.DoesNotContain(board.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Entries), e => e.Name == boss.Name);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void Timer_tile_identifies_the_next_label_and_empty_schedule() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Roses." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var timer = services.Timers.Current.Timers.Single(t => t.Preset == Presets.WarOfTheRoses);
            var tile = new TimerTileViewModel(timer, services, new PanelHost(), new(2026, 10, 4, 14, 0, 0, TimeSpan.Zero));
            Assert.StartsWith("Next Battle · ", tile.Detail);
            tile.SetTimer(timer with { Scheduled = timer.Scheduled! with { Slots = [] } }, services.Clock.UtcNow);
            Assert.Equal("No times set", tile.Detail);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void War_of_the_Roses_loads_bundled_picture_without_error() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Roses." + Guid.NewGuid().ToString("N"));
        try
        {
            BdoTimers.Core.Diagnostics.Log.Init(path, new FixedClock());
            var art = new ArtLibrary(path);
            var picture = art.For(Presets.Create().Single(t => t.Preset == Presets.WarOfTheRoses));
            var logs = Directory.GetFiles(path, "*.log");
            Assert.True(logs.Length == 0, string.Join(Environment.NewLine, logs.Select(File.ReadAllText)));
            var bitmap = Assert.IsType<System.Windows.Media.Imaging.BitmapImage>(picture.Source);
            Assert.Equal(351, bitmap.PixelWidth);
            Assert.Equal(318, bitmap.PixelHeight);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    sealed class PanelHost(Action? complete = null) : IPanelHost
    {
        public void OpenPanel(object panel) { }
        public void ClosePanel() { }
        public bool IsOpen(object panel) => true;
        public void CompletePanelEdits() => complete?.Invoke();
    }
}
