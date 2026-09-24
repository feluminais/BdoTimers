using System.Diagnostics;
using System.IO;
using System.Windows;
using BdoTimers.App.Alerts;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

public sealed class AppServices : IDisposable
{
    readonly Application _app;
    readonly SchedulerEngine _engine;
    readonly SchedulerLoop _loop;
    readonly TrayIcon _tray;
    readonly ToastChannel _toast;
    readonly SoundChannel _sound = new();
    readonly string _dataDir;
    MainWindow? _main;

    public TimerStore Timers { get; }
    public PersistentState<AppSettings> Settings { get; }
    public BossSeed Seed { get; }
    public UiClock UiClock { get; } = new();
    public IAlertSink Alerts { get; }
    public TtsChannel Tts { get; }
    public OverlayController Overlay { get; }
    public ArtLibrary Art { get; }
    public bool IsQuitting { get; private set; }
    IReadOnlyList<string> RecoveredFiles { get; }

    public AppServices(Application app, string dataDir)
    {
        _app = app;
        _dataDir = dataDir;
        Art = new ArtLibrary(Path.Combine(dataDir, "images"));
        var settingsFile = new JsonFileStore<AppSettings>(Path.Combine(dataDir, "settings.json"), () => new AppSettings());
        var timersFile = new JsonFileStore<AppData>(Path.Combine(dataDir, "timers.json"), () => new AppData());
        var settings = settingsFile.Load();
        var timers = timersFile.Load();
        RecoveredFiles = new[] { settings.RecoveredBackupPath, timers.RecoveredBackupPath }.OfType<string>().ToList();

        Settings = new PersistentState<AppSettings>(settingsFile, settings.Value);
        Timers = new TimerStore(timersFile, timers.Value);
        Seed = SeedService.LoadEmbedded();
        Timers.Update(d => SeedService.ApplyIfNeeded(d, Seed, DefaultAlerts()));

        _toast = new ToastChannel();
        _toast.Activated += () => _app.Dispatcher.BeginInvoke(ShowMainWindow);
        Tts = new TtsChannel();
        Alerts = new AlertDispatcher(_toast, _sound, Tts, Settings);
        _engine = new SchedulerEngine(Timers, Settings, Alerts, new SystemClock());
        _loop = new SchedulerLoop(_engine);
        _tray = new TrayIcon(this);
        Overlay = new OverlayController(this);
    }

    public AlertConfig DefaultAlerts() => new() { LeadTimesMinutes = Settings.Current.DefaultLeadTimesMinutes };

    public void Start(bool showWindow)
    {
        _engine.ReconcileStartup();
        _loop.Start();
        UiClock.Start();
        Overlay.Start();
        foreach (var path in RecoveredFiles)
            _toast.ShowInfo("A data file was damaged",
                $"BDO Timers started with defaults. The damaged file was kept as {Path.GetFileName(path)}.");
        if (!Settings.Current.PriorityHintShown)
        {
            _toast.ShowInfo("Let alerts through while gaming",
                "Add BDO Timers to Settings → Notifications → Set priority notifications.",
                withNotificationSettingsButton: true);
            Settings.Update(s => s with { PriorityHintShown = true });
        }
        Autostart.Apply(Settings.Current.Autostart);
        if (showWindow) ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        _main ??= new MainWindow(new MainViewModel(this), Settings);
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void PauseAlerts(TimeSpan? duration) =>
        Settings.Update(s => AlertPause.Pause(s, DateTimeOffset.UtcNow, duration));

    public void ResumeAlerts() => Settings.Update(AlertPause.Resume);

    public void SendTestAlert() => Alerts.Dispatch(new AlertEvent(
        [new TimerDef { Name = "Test boss", Alerts = DefaultAlerts() }], DateTimeOffset.UtcNow.AddMinutes(5), 5, 5));

    public async void PreviewSound()
    {
        try { await _sound.PlayAsync(null, Settings.Current.Volume); }
        catch (Exception ex) { Log.Error("Sound preview failed", ex); }
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(_dataDir);
        Process.Start(new ProcessStartInfo(_dataDir) { UseShellExecute = true });
    }

    public void ResetBossTimetable() => Timers.Update(d => SeedService.ResetBuiltIns(d, Seed, DefaultAlerts()));

    public void Quit()
    {
        IsQuitting = true;
        _app.Shutdown();
    }

    public void Dispose()
    {
        UiClock.Stop();
        _loop.Dispose();
        _toast.Dispose();
        Tts.Dispose();
        _tray.Dispose();
    }
}
