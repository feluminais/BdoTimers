using System.IO;
using System.Windows;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;
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
    MainWindow? _main;

    public TimerStore Timers { get; }
    public PersistentState<AppSettings> Settings { get; }
    public BossSeed Seed { get; }
    public UiClock UiClock { get; } = new();
    public IAlertSink Alerts { get; }
    public bool IsQuitting { get; private set; }
    IReadOnlyList<string> RecoveredFiles { get; }

    public AppServices(Application app, string dataDir)
    {
        _app = app;
        var settingsFile = new JsonFileStore<AppSettings>(Path.Combine(dataDir, "settings.json"), () => new AppSettings());
        var timersFile = new JsonFileStore<AppData>(Path.Combine(dataDir, "timers.json"), () => new AppData());
        var settings = settingsFile.Load();
        var timers = timersFile.Load();
        RecoveredFiles = new[] { settings.RecoveredBackupPath, timers.RecoveredBackupPath }.OfType<string>().ToList();

        Settings = new PersistentState<AppSettings>(settingsFile, settings.Value);
        Timers = new TimerStore(timersFile, timers.Value);
        Seed = SeedService.LoadEmbedded();
        Timers.Update(d => SeedService.ApplyIfNeeded(d, Seed, DefaultAlerts()));

        Alerts = new NullAlertSink();
        _engine = new SchedulerEngine(Timers, Settings, Alerts, new SystemClock());
        _loop = new SchedulerLoop(_engine);
        _tray = new TrayIcon(this);
    }

    public AlertConfig DefaultAlerts() => new() { LeadTimesMinutes = Settings.Current.DefaultLeadTimesMinutes };

    public void Start(bool showWindow)
    {
        _engine.ReconcileStartup();
        _loop.Start();
        UiClock.Start();
#if !DEBUG
        Autostart.Apply(Settings.Current.Autostart);
#endif
        if (showWindow) ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        _main ??= new MainWindow(new MainViewModel(this));
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void PauseAlerts(TimeSpan? duration) => Settings.Update(s => s with
    {
        AlertsPausedUntilUtc = duration is { } d ? DateTimeOffset.UtcNow + d : DateTimeOffset.MaxValue,
    });

    public void ResumeAlerts() => Settings.Update(s => s with { AlertsPausedUntilUtc = null });

    public void Quit()
    {
        IsQuitting = true;
        _app.Shutdown();
    }

    public void Dispose()
    {
        UiClock.Stop();
        _loop.Dispose();
        _tray.Dispose();
    }

    sealed class NullAlertSink : IAlertSink
    {
        public void Dispatch(AlertEvent alert) { }
        public void NotifyEndedWhileAway(TimerDef timer) { }
    }
}
