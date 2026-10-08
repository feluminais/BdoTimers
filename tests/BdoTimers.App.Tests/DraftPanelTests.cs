using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using BdoTimers.App.Views.Panels;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Tests;

public class DraftPanelTests
{
    [Fact]
    public void GuildBossActiveSwitchStaysPrivateUntilSaveAndKeepsItsWeeklyTime() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            foreach (var peer in services.Timers.Current.Timers)
                services.Timers.Modify(peer.Id, t => t with { Alerts = t.Alerts with { Tts = t.Alerts.Tts with { Enabled = false } } });
            var timer = services.Timers.Current.Timers.Single(t => t.Preset == BdoTimers.Core.Seed.Presets.GuildBosses);
            using var main = new MainViewModel(services);
            var panel = new CustomPanelViewModel(services, main, timer);
            main.OpenPanel(panel);
            Assert.True(panel.HasActive);
            Assert.False(panel.ShowsWeekly);
            Assert.False(panel.HasTimeZone);
            panel.Active = true;
            Assert.True(panel.ShowsWeekly);
            Assert.True(panel.HasTimeZone);
            Assert.True(services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.Off);
            main.FinishPanel().GetAwaiter().GetResult();
            var saved = services.Timers.Current.Timers.Single(t => t.Id == timer.Id);
            Assert.False(saved.Scheduled!.Off);
            Assert.Equal(timer.Scheduled!.Slots, saved.Scheduled.Slots);
            panel = new CustomPanelViewModel(services, main, saved);
            main.OpenPanel(panel);
            panel.Active = false;
            main.ClosePanel();
            Assert.True(main.AskingDiscard);
            main.DiscardChangesCommand.Execute(null);
            Assert.False(services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.Off);
        });
    });

    [Fact]
    public void DismissWarnsKeepEditingRetainsDraftAndDiscardKeepsSavedTimer() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var timer = Timer();
            services.Timers.Upsert(timer);
            using var main = new MainViewModel(services);
            var panel = new CustomPanelViewModel(services, main, timer);
            main.OpenPanel(panel);
            panel.Name = "New title";
            panel.Alerts.VoiceLine = "[name] will finish in [time]";
            Assert.Equal("Old title", services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Name);
            main.ClosePanel();
            Assert.True(main.AskingDiscard);
            Assert.Same(panel, main.Panel);
            main.KeepEditingCommand.Execute(null);
            Assert.False(main.AskingDiscard);
            Assert.Equal("New title", panel.Name);
            main.ClosePanel();
            main.DiscardChangesCommand.Execute(null);
            Assert.Null(main.Panel);
            Assert.Equal("Old title", services.Timers.Current.Timers.Single(t => t.Id == timer.Id).Name);
        });
    });

    [Fact]
    public void SaveCommitsTitleWithoutRevertingCountdownStartedDuringEditing() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var timer = Timer();
            services.Timers.Upsert(timer);
            using var main = new MainViewModel(services);
            var panel = new CustomPanelViewModel(services, main, timer);
            main.OpenPanel(panel);
            panel.Name = "New title";
            var ends = services.Clock.UtcNow.AddMinutes(10);
            services.Timers.Modify(timer.Id, t => t with { Countdown = t.Countdown! with { Status = CountdownStatus.Running, EndsAtUtc = ends } });
            main.FinishPanel().GetAwaiter().GetResult();
            Assert.Null(main.Panel);
            var saved = services.Timers.Current.Timers.Single(t => t.Id == timer.Id);
            Assert.Equal("New title", saved.Name);
            Assert.Equal(ends, saved.Countdown!.EndsAtUtc);
            Assert.Equal(CountdownStatus.Running, saved.Countdown.Status);
        });
    });

    [Fact]
    public void SettingsVoiceAndSpeedStayPrivateUntilSavedOrDiscarded() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var original = services.Settings.Current;
            var panel = new SettingsPanelViewModel(services);
            panel.Voice = panel.Voices.Last();
            panel.SpeechRate = 3;
            Assert.True(panel.HasChanges);
            Assert.Equal(original.TtsVoice, services.Settings.Current.TtsVoice);
            Assert.Equal(original.TtsRate, services.Settings.Current.TtsRate);
            panel.OnClosed();
        });
    });

    [Fact]
    public void GenerateFlushesTheVoiceFieldBeforeItsDelayExpires() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var timer = Timer();
            services.Timers.Upsert(timer);
            using var main = new MainViewModel(services);
            var panel = new CustomPanelViewModel(services, main, timer);
            var view = new CustomPanel { DataContext = panel };
            var window = new Window { Content = view, Width = 460, Height = 800 };
            try
            {
                panel.Alerts.EditingVoiceLine = true;
                window.Show();
                WpfTest.Drain();
                var box = PanelFocusScope.Descendants(view).OfType<TextBox>().Single(b => new TextBoxAutomationPeer(b).GetName() == "Voice line");
                box.Text = "[name] is ready in [time]";
                Assert.NotEqual(box.Text, panel.Alerts.VoiceLine);
                var generate = PanelFocusScope.Descendants(view).OfType<Button>().Single(b => Equals(b.Content, "Generate"));
                generate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WpfTest.Drain();
                Assert.Equal(box.Text, panel.Alerts.VoiceLine);
                Assert.Contains("is ready", panel.Alerts.VoiceSample);
            }
            finally { window.Close(); panel.OnClosed(); }
        });
    });

    [Fact]
    public void DurationEditPreservesAStartThatHappenedAfterOpeningTheEditor() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var timer = Timer();
            services.Timers.Upsert(timer);
            using var main = new MainViewModel(services);
            var panel = new CustomPanelViewModel(services, main, timer);
            main.OpenPanel(panel);
            panel.DurationText = "0:02";
            var started = services.Clock.UtcNow;
            services.Timers.Modify(timer.Id, t => t with { Countdown = BdoTimers.Core.Scheduling.CountdownOps.Start(t.Countdown!, started) });
            main.FinishPanel().GetAwaiter().GetResult();
            var saved = services.Timers.Current.Timers.Single(t => t.Id == timer.Id);
            Assert.Equal(started + TimeSpan.FromMinutes(2), saved.Countdown!.EndsAtUtc);
        });
    });

    [Fact]
    public void WindowCloseContinuationRunsOnlyAfterDiscard() => WpfTest.Run(() =>
    {
        var main = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        typeof(MainViewModel).GetField("<Saving>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(main, new EditorSave());
        main.Panel = new DirtyPanel();
        var closed = false;
        Assert.False(main.RequestLeave(() => closed = true, runImmediately: false));
        Assert.False(closed);
        main.DiscardChangesCommand.Execute(null);
        Assert.True(closed);
        Assert.Null(main.Panel);
        closed = false;
        Assert.True(main.RequestLeave(() => closed = true, runImmediately: false));
        Assert.False(closed);
    });

    [Fact]
    public void CancellingRestoreDoesNotArmARestartAndDiscardsPreparedFiles() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            using var main = new MainViewModel(services);
            main.Panel = new DirtyPanel();
            typeof(AppServices).GetField("_mainViewModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, main);
            // Use the restore preparer's own folder convention.
            var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Restore." + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var archive = Path.Combine(root, "backup.zip");
                services.ExportBackup(archive, "1.0.1");
                var prepared = BackupArchive.Prepare(archive, root);
                services.RestartForRestore(prepared);
                Assert.True(main.AskingDiscard);
                Assert.True(Directory.Exists(prepared.Directory));
                main.KeepEditingCommand.Execute(null);
                Assert.False(Directory.Exists(prepared.Directory));
                Assert.False(services.IsQuitting);
                main.DiscardChangesCommand.Execute(null);
            }
            finally { Directory.Delete(root, true); }
        });
    });

    [Fact]
    public void ActualEditorShowsGenerateSaveAndDiscardControls() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            var timer = Timer();
            services.Timers.Upsert(timer);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720 };
            var panel = new CustomPanelViewModel(services, main, timer);
            try
            {
                window.Show();
                main.OpenPanel(panel);
                panel.Alerts.EditingVoiceLine = true;
                panel.Name = "Edited title";
                WpfTest.Drain();
                var buttons = PanelFocusScope.Descendants(window).OfType<Button>().Where(b => b.IsVisible).ToArray();
                Assert.Contains(buttons, b => Equals(b.Content, "Generate"));
                Assert.Contains(buttons, b => Equals(b.Content, "Save"));
                var nameBox = PanelFocusScope.Descendants(window).OfType<TextBox>()
                    .Single(b => b.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Name");
                var voiceBox = PanelFocusScope.Descendants(window).OfType<TextBox>()
                    .Single(b => new TextBoxAutomationPeer(b).GetName() == "Voice line");
                nameBox.Text = "Latest title";
                voiceBox.Text = "Soon: [name]";
                Assert.Equal("Edited title", panel.Name);
                buttons.Single(b => Equals(b.Content, "Generate")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Latest title", panel.Name);
                Assert.Equal("Soon: [name]", panel.Alerts.VoiceLine);
                Assert.Contains("Latest title", panel.Alerts.VoiceSample);
                Capture(window, "voice-editor.png");
                main.ClosePanel();
                WpfTest.Drain();
                Assert.True(main.AskingDiscard);
                Assert.Contains(PanelFocusScope.Descendants(window).OfType<Button>(), b => b.IsVisible && b.IsEnabled && Equals(b.Content, "Keep editing"));
                Capture(window, "voice-discard.png");
                main.DiscardChangesCommand.Execute(null);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        });
    });

    static void Capture(Window window, string name) => UiCapture.Save(window, name);

    [Fact]
    public void Panels_open_as_a_drawer_and_settings_as_a_sheet() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            using var main = new MainViewModel(services);
            Assert.Equal(PanelPresentation.Drawer, ((IPanel)new DirtyPanel()).Presentation);
            main.OpenSettingsCommand.Execute(null);
            Assert.Equal(PanelPresentation.Sheet, main.PanelPresentation);
            main.ClosePanel();
            Assert.Equal(PanelPresentation.Sheet, main.PanelPresentation);
            main.OpenPanel(new QuietPanel());
            Assert.Equal(PanelPresentation.Drawer, main.PanelPresentation);
        });
    });

    [Fact]
    public void The_drawer_sits_at_the_right_edge_and_the_sheet_in_the_middle() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.OpenPanel(new QuietPanel());
                WpfTest.Drain();
                var frame = (FrameworkElement)window.FindName("PanelFrame");
                Assert.Equal(HorizontalAlignment.Right, frame.HorizontalAlignment);
                Assert.Equal(VerticalAlignment.Stretch, frame.VerticalAlignment);
                main.ClosePanel();
                WpfTest.Drain();
                main.OpenSettingsCommand.Execute(null);
                WpfTest.Drain();
                Assert.Equal(HorizontalAlignment.Center, frame.HorizontalAlignment);
                Assert.Equal(VerticalAlignment.Center, frame.VerticalAlignment);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        });
    });

    [Fact]
    public void The_bell_pauses_for_an_hour_or_until_resumed_and_resumes() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            using var main = new MainViewModel(services);
            Assert.False(main.IsPaused);
            main.PauseHourCommand.Execute(null);
            Assert.True(main.IsPaused);
            Assert.StartsWith("Alerts paused", main.PausedText);
            Assert.NotEqual("Alerts paused", main.PausedText);
            main.ResumeCommand.Execute(null);
            Assert.False(main.IsPaused);
            Assert.Equal("", main.PausedText);
            main.PauseUntilResumedCommand.Execute(null);
            Assert.True(main.IsPaused);
            Assert.Equal("Alerts paused", main.PausedText);
        });
    });

    [Fact]
    public void The_Garmoth_tracker_is_off_until_it_is_saved_on_in_Settings_and_saving_it_off_clears_the_week() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            foreach (var timer in services.Timers.Current.Timers)
                services.Timers.Modify(timer.Id, t => t with { Alerts = t.Alerts with { Tts = t.Alerts.Tts with { Enabled = false } } });
            using var main = new MainViewModel(services);
            var panel = new SettingsPanelViewModel(services);
            Assert.False(panel.GarmothTracker);
            Assert.False(panel.HasChanges);

            panel.GarmothTracker = true;
            Assert.True(panel.HasChanges);
            Assert.False(services.Settings.Current.GarmothTracker);
            main.OpenPanel(panel);
            main.FinishPanel().GetAwaiter().GetResult();

            Assert.True(services.Settings.Current.GarmothTracker);
            Assert.True(services.Timers.Current.Garmoth.ResetUtc > services.Clock.UtcNow);

            services.Timers.MarkGarmoth(3, services.Clock.UtcNow);
            Assert.Equal(3, services.Timers.Current.Garmoth.Kills);
            panel = new SettingsPanelViewModel(services);
            Assert.True(panel.GarmothTracker);
            panel.GarmothTracker = false;
            main.OpenPanel(panel);
            main.FinishPanel().GetAwaiter().GetResult();

            Assert.False(services.Settings.Current.GarmothTracker);
            Assert.Equal(new GarmothWeek(), services.Timers.Current.Garmoth);
        });
    });

    [Fact]
    public void Overlay_options_apply_at_once_and_neither_count_as_unsaved_settings_nor_get_undone_by_Save() => WpfTest.Run(() =>
    {
        WithServices(services =>
        {
            foreach (var timer in services.Timers.Current.Timers)
                services.Timers.Modify(timer.Id, t => t with { Alerts = t.Alerts with { Tts = t.Alerts.Tts with { Enabled = false } } });
            using var main = new MainViewModel(services);
            var panel = new SettingsPanelViewModel(services);
            main.OpenPanel(panel);

            panel.Overlay.Scale = 1.4;
            Assert.Equal(1.4, services.Settings.Current.Overlay.Scale);
            Assert.False(panel.HasChanges);
            main.ClosePanel();
            Assert.False(main.AskingDiscard);
            Assert.Null(main.Panel);

            panel = new SettingsPanelViewModel(services);
            main.OpenPanel(panel);
            panel.CloseToTray = !panel.CloseToTray;
            panel.Overlay.Scale = 1.8;
            Assert.True(panel.HasChanges);
            main.FinishPanel().GetAwaiter().GetResult();

            Assert.Equal(1.8, services.Settings.Current.Overlay.Scale);
            Assert.Equal(panel.CloseToTray, services.Settings.Current.CloseToTray);
        });
    });

    static TimerDef Timer() => new() { Name = "Old title", Kind = TimerKind.Countdown, Countdown = new(), Alerts = new() { Tts = new() { Enabled = false } } };
    static void WithServices(Action<AppServices> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Draft." + Guid.NewGuid());
        try { using var services = new AppServices(Application.Current, root); test(services); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    sealed class QuietPanel : IPanel { }
    sealed class DirtyPanel : IDraftPanel
    {
        public bool HasChanges => true;
        public Task SaveAsync() => Task.CompletedTask;
    }
}
