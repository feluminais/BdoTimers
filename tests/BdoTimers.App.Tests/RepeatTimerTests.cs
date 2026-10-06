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
    public void Panel_saves_repeat_explicitly_validates_all_fields_and_reload_restores_region_defaults() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Roses." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var timer = services.Timers.Current.Timers.Single(t => t.Preset == Presets.WarOfTheRoses);
            services.Timers.Modify(timer.Id, t => t with { Alerts = t.Alerts with { Tts = t.Alerts.Tts with { Enabled = false } } });
            timer = services.Timers.Current.Timers.Single(t => t.Id == timer.Id);
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
                Assert.Equal(2, services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.EveryWeeks);
                panel.SaveAsync().GetAwaiter().GetResult();
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
                Assert.Equal(3, services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.EveryWeeks);
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
                var finish = PanelFocusScope.Descendants(view).OfType<Button>().Single(b => Equals(b.Content, "Save") && b.DataContext == panel);
                Assert.True(finish.IsEnabled);
                panel.Slots.Rows[1].TimeText = "bad";
                WpfTest.Drain();
                Assert.False(finish.IsEnabled);
                panel.Slots.Rows[1].TimeText = "16:00";
                WpfTest.Drain();
                Assert.True(finish.IsEnabled);
                panel.SaveAsync().GetAwaiter().GetResult();
                Assert.Equal(new TimeOnly(16, 0), services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.Slots[1].Time);
            }
            finally { window.Close(); panel.OnClosed(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void Removing_an_event_boss_discards_pending_edits_and_undo_restores_saved_name() => WpfTest.Run(() =>
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
            Assert.Equal(boss.Name, services.Timers.Current.Timers.Single(t => t.Id == boss.Id).Name);
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
    public void Timer_cards_show_start_at_rest_and_keep_stop_and_skip_in_a_menu() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Cards." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            services.Timers.Upsert(new TimerDef { Name = "Buffs", Kind = TimerKind.Countdown, Countdown = new() });
            var timers = new CustomViewModel(services, new PanelHost());
            var tiles = timers.Items.ToList();
            Assert.All(tiles, tile => Assert.Equal(tile.HasControls || tile.HasNextOccurrence, tile.HasMore));
            Assert.Contains(tiles, tile => tile.HasControls);
            Assert.Contains(tiles, tile => tile.HasNextOccurrence);
            var view = new CustomView { DataContext = timers };
            var window = new Window { Content = view, Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                var newTimer = BdoTimers.App.Controls.VisualTree.FindDescendant<Button>(view, b => b.Command == timers.NewTimerCommand)!;
                Assert.Equal("New timer", new ButtonAutomationPeer(newTimer).GetName());
                foreach (var tile in tiles.Where(t => t.HasControls))
                {
                    var start = BdoTimers.App.Controls.VisualTree.FindDescendant<Button>(view, b => b.Command == tile.StartPauseCommand)!;
                    Assert.True(start.IsVisible && start.Opacity == 1 && start.IsHitTestVisible, $"{tile.Name}: start is there without hover");
                    var started = BdoTimers.App.Controls.VisualTree.FindDescendant<Button>(view, b => b.Command == tile.PickStartCommand)!;
                    Assert.Equal(0, started.Opacity);
                }
                var stoppable = tiles.First(t => t.HasControls);
                var more = BdoTimers.App.Controls.VisualTree.FindDescendant<Button>(view,
                    b => b.DataContext == stoppable && System.Windows.Automation.AutomationProperties.GetName(b) == "More")!;
                Assert.Equal(0, more.Opacity);
                more.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                WpfTest.Drain();
                Assert.True(more.ContextMenu!.IsOpen);
                Assert.Equal(1, more.Opacity);
                Assert.Contains(more.ContextMenu.Items.OfType<MenuItem>(), item => item.Command == stoppable.ResetCommand);
                more.ContextMenu.IsOpen = false;
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Theory]
    [InlineData(Presets.WarOfTheRoses, 351, 318)]
    [InlineData(Presets.GuildBosses, 960, 540)]
    public void Preset_loads_bundled_picture_without_error(string preset, int width, int height) => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Roses." + Guid.NewGuid().ToString("N"));
        try
        {
            BdoTimers.Core.Diagnostics.Log.Init(path, new FixedClock());
            var art = new ArtLibrary(path);
            var picture = art.For(Presets.Create().Single(t => t.Preset == preset));
            var logs = Directory.GetFiles(path, "*.log");
            Assert.True(logs.Length == 0, string.Join(Environment.NewLine, logs.Select(File.ReadAllText)));
            var bitmap = Assert.IsType<System.Windows.Media.Imaging.BitmapImage>(picture.Source);
            Assert.Equal(width, bitmap.PixelWidth);
            Assert.Equal(height, bitmap.PixelHeight);
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
