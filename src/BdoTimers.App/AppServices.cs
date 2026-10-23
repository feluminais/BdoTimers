using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using BdoTimers.App.Alerts;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.App.Theme;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Updates;
using Microsoft.Win32;

namespace BdoTimers.App;

public sealed class AppServices : IDisposable
{
    readonly Application _app;
    readonly SchedulerEngine _engine;
    readonly SchedulerLoop _loop;
    readonly TrayIcon _tray;
    readonly ToastChannel _toast;
    readonly SoundChannel _sound;
    readonly AlertDispatcher _alerts;
    readonly IReadOnlyDictionary<string, BossSeed> _seeds;
    readonly string _dataDir;
    MainWindow? _main;
    MainViewModel? _mainViewModel;
    readonly CancellationTokenSource _updateShutdown = new();
    readonly HttpClient _updateHttp = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public TimerStore Timers { get; }
    public TodoStore Todos { get; }
    public PersistentState<AppSettings> Settings { get; }
    public BossRegion Region => BossRegions.Find(Timers.Current.SelectedBossRegion);
    public BossSeed Seed => _seeds[Timers.Current.SelectedBossRegion];
    public UiClock UiClock { get; }
    /// <summary>The boss board shared by the overlay, Today and the Schedule.</summary>
    public BossBoardCache Boards { get; } = new();
    public IClock Clock { get; } = new SystemClock();
    public TtsChannel Tts { get; }
    public OverlayController Overlay { get; }
    public ArtLibrary Art { get; }
    public UserSounds Sounds { get; }
    public UpdateChecker Updates { get; }
    public bool IsQuitting { get; private set; }
    public AppHealth Health { get; }
    public UndoService Undo { get; }
    public AudioPreview Audio { get; }
    public UserFiles Files { get; }
    public SupportTools Support { get; }
    public BossTimetable Timetable { get; }
    public AgentHost Agent { get; }
    readonly ThemePreferences _theme;
    IReadOnlyList<string> RecoveredFiles { get; }

    public AppServices(Application app, string dataDir)
    {
        _app = app;
        _dataDir = dataDir;
        Health = new AppHealth(Clock, app.Dispatcher);
        UiClock = new UiClock(Clock);
        Art = new ArtLibrary(Path.Combine(dataDir, "images"));
        Sounds = new UserSounds(Path.Combine(dataDir, "sounds"));
        _sound = new SoundChannel(Sounds);
        _seeds = BossRegions.All.ToDictionary(r => r.Id, r => SeedService.LoadEmbedded(r.Id));
        var settingsFile = new JsonFileStore<AppSettings>(Path.Combine(dataDir, "settings.json"), () => new AppSettings());
        var timersFile = new JsonFileStore<AppData>(Path.Combine(dataDir, "timers.json"), () => SeedService.NewData(_seeds[BossRegions.Europe]));
        var settings = settingsFile.Load();
        var timers = timersFile.Load();
        var todosFile = new JsonFileStore<TodoData>(Path.Combine(dataDir, "todos.json"),
            () => TodoSeed.Create(Clock.UtcNow, settings.Value));
        var todos = todosFile.Load();
        // Check every format before any migrations or initial saves can overwrite user state.
        if (!File.Exists(timersFile.FilePath)) timersFile.Save(timers.Value);
        if (!File.Exists(todosFile.FilePath)) todosFile.Save(todos.Value);
        Settings = new PersistentState<AppSettings>(settingsFile, settings.Value);
        _theme = ThemePreferences.Initialize(app, Settings);
        RecoveredFiles = new[] { settings.RecoveredBackupPath, timers.RecoveredBackupPath, todos.RecoveredBackupPath }
            .OfType<string>().ToList();
        Todos = new TodoStore(todosFile, todos.Value, Clock);
        Settings.Changed += () => Health.Saved(settingsFile.FilePath);
        Todos.Changed += () => Health.Saved(todosFile.FilePath);
        Todos.Update(TodoMigrations.Apply);
        Timers = new TimerStore(timersFile, timers.Value);
        Timers.Changed += () => Health.Saved(timersFile.FilePath);
        Timers.Update(d =>
        {
            var migrated = DataMigrations.Apply(d, Settings.Current);
            return Presets.Ensure(SeedService.ApplyIfNeeded(migrated, _seeds[migrated.SelectedBossRegion], new AlertConfig()));
        });
        FollowSettings();
        Undo = new UndoService(Timers, Todos, Clock, file =>
        {
            if (Timers.Current.Timers.All(t => t.ImageFile != file) && Settings.Current.Overlay.BackgroundImage != file)
                Art.Delete(file);
        });

        _toast = new ToastChannel(App.InstanceName, App.DisplayName);
        _toast.Activated += () => _app.Dispatcher.BeginInvoke(ShowMainWindow);
        Tts = new TtsChannel(new KokoroEngine(Path.Combine(AppContext.BaseDirectory, "Voice", "kokoro")),
            new SpeechCache(Path.Combine(dataDir, "speech"), Path.Combine(AppContext.BaseDirectory, "Voice", "speech")));
        Tts.CleanCacheInBackground();
        _alerts = new AlertDispatcher(_toast, _sound, Tts, Settings, Sounds, Timers, Health);
        Audio = new AudioPreview(_sound, Tts, Sounds, Settings, Health);
        Files = new UserFiles(Art, Sounds, Timers, Settings);
        Support = new SupportTools(_alerts, Health, Clock, dataDir, _updateShutdown.Token);
        Timetable = new BossTimetable(_seeds, Timers, Undo, _toast, Clock);
        _engine = new SchedulerEngine(Timers, Settings, _alerts, Clock);
        _loop = new SchedulerLoop(_engine, Clock,
            onFailure: ex => Health.Failed(ex is StateSaveException ? "Saving" : "Scheduler", ex),
            onRecovered: _ => Health.Succeeded("Scheduler"));
        _tray = new TrayIcon(this);
        Overlay = new OverlayController(this);
        Agent = new AgentHost(this, app.Dispatcher);

        Updates = CreateUpdateChecker(dataDir);
    }

    /// <summary>The to-do lists and the Garmoth tracker's week follow the reset times and the switch, which change in Settings.</summary>
    void FollowSettings()
    {
        Settings.Changed += () =>
        {
            try { Todos.Reconcile(Settings.Current); }
            catch (StateSaveException ex) { Log.Error("Couldn't save to-do reset settings", ex); Health.Failed("Saving", ex); }
        };
        Settings.Changed += () =>
        {
            try { Timers.ReconcileGarmoth(Settings.Current, Clock.UtcNow); }
            catch (StateSaveException ex) { Log.Error("Couldn't save the Garmoth tracker", ex); Health.Failed("Saving", ex); }
        };
    }

    UpdateChecker CreateUpdateChecker(string dataDir)
    {
        var updateFile = new JsonFileStore<UpdateCheckState>(Path.Combine(dataDir, "update-check.json"), () => new());
        UpdateCheckState updateState;
        try { updateState = updateFile.Load().Value; }
        catch (Exception ex)
        {
            Log.Error("Couldn't load update check timing", ex);
            updateState = new();
        }
        return new UpdateChecker(new GitHubReleaseSource(_updateHttp), ProductVersion.Number, Clock,
            new PersistentState<UpdateCheckState>(updateFile, updateState), App.AutomaticUpdateChecks,
            shutdown: _updateShutdown.Token);
    }

    /// <summary>Optional steps log their failures and carry on, so none of them can stop the app from starting.</summary>
    public void Start(bool showWindow, bool measuring = false)
    {
        ReconcileStartup();
        var todoResetErrors = new RepeatingErrorLog("To-do reset", Clock);
        UiClock.Tick += _ =>
        {
            Undo.Refresh();
            try
            {
                Todos.Reconcile(Settings.Current);
                Timers.ReconcileGarmoth(Settings.Current, Clock.UtcNow);
                todoResetErrors.Succeeded();
            }
            catch (StateSaveException ex) { todoResetErrors.Failed(ex); Health.Failed("Saving", ex); }
        };
        // .NET caches the local time zone; without this a zone change would show old local times until a restart.
        SystemEvents.TimeChanged += OnTimeChanged;
        _loop.Start();
        UiClock.Start();
        Overlay.Start();
        if (measuring) { EcoQos.Set(true); return; }
        Agent.Apply(Settings.Current.AgentAccess);
        Settings.Changed += () => Agent.Apply(Settings.Current.AgentAccess);
        NotifyStartupHints();
        try { Autostart.Apply(Settings.Current.Autostart); }
        catch (Exception ex) { Log.Error("Couldn't update autostart", ex); }
        Timetable.NotifyReview();
        if (showWindow) ShowMainWindow();
        else EcoQos.Set(true);
#if !DEBUG
        _ = Task.Run(CheckForUpdatesOnStartup);
#endif
    }

    void ReconcileStartup()
    {
        try { _engine.ReconcileStartup(); }
        catch (Exception ex) { Log.Error("Couldn't complete countdowns that ended while closed", ex); Health.Failed("Startup", ex); }
        try { Todos.Reconcile(Settings.Current); }
        catch (StateSaveException ex) { Log.Error("Couldn't save to-do lists", ex); Health.Failed("Saving", ex); }
        try { Timers.ReconcileGarmoth(Settings.Current, Clock.UtcNow); }
        catch (StateSaveException ex) { Log.Error("Couldn't save the Garmoth tracker", ex); Health.Failed("Saving", ex); }
    }

    void NotifyStartupHints()
    {
        foreach (var path in RecoveredFiles)
            _toast.ShowInfo("A data file was damaged",
                $"BDO Timers started with defaults. The damaged file was kept as {Path.GetFileName(path)}.");
        // Marked as shown only once it was, so a failure brings it back at the next start.
        if (!Settings.Current.NotificationHintShown
            && _toast.ShowInfo("Let alerts through while gaming",
                "Add BDO Timers to Settings → Notifications → Set priority notifications.",
                withNotificationSettingsButton: true))
        {
            try { Settings.Update(s => s with { NotificationHintShown = true }); }
            catch (Exception ex) { Log.Error("Couldn't save that the notification hint was shown", ex); }
        }
    }

    static void OnTimeChanged(object? sender, EventArgs e) => TimeZoneInfo.ClearCachedData();

    async Task CheckForUpdatesOnStartup()
    {
        try
        {
            await Updates.CheckAutomaticallyAsync();
        }
        catch (Exception ex) { Log.Error("Startup update check failed", ex); }
    }

    public void ShowMainWindow()
    {
        var main = EnsureMainWindow();
#if DEBUG
        NativeMethods.ReleaseBottom(new System.Windows.Interop.WindowInteropHelper(main).EnsureHandle());
#endif
        main.Show();
        if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
        main.Activate();
    }

#if DEBUG
    /// <summary>Shows the window unfocused and keeps it behind every other one, so UI checks can read and
    /// screenshot a dev build without covering the game.</summary>
    public void ShowMainWindowBehind()
    {
        var main = EnsureMainWindow();
        var hwnd = new System.Windows.Interop.WindowInteropHelper(main).EnsureHandle();
        System.Windows.Interop.HwndSource.FromHwnd(hwnd).AddHook(NativeMethods.KeepAtBottom);
        main.ShowActivated = false;
        main.Show();
    }
#endif

    MainWindow EnsureMainWindow()
    {
        if (_main is null)
        {
            _mainViewModel = new MainViewModel(this);
            _main = new MainWindow(_mainViewModel, this);
            _main.StartWarmUp();
        }
        return _main;
    }

    public void PauseAlerts(TimeSpan? duration) =>
        Settings.Update(s => AlertPause.Pause(s, Clock.UtcNow, duration));

    /// <summary>Brings the window up with Settings open on Overlay.</summary>
    public void ShowOverlaySettings()
    {
        ShowMainWindow();
        _mainViewModel?.OpenOverlaySettings();
    }

    public void ResumeAlerts() => Settings.Update(AlertPause.Resume);

    public void ControlCustomCountdown(Guid id) => Timers.ControlCustomCountdown(id, Clock);

    public HorseStartResult StartHorseRegistration(bool announce)
    {
        var result = Timers.StartHorseRegistration(Clock.UtcNow);
        if (result == HorseStartResult.Started && announce) _alerts.Say("Horse registration time started");
        else if (result == HorseStartResult.LimitReached)
            _toast.ShowInfo("Horse registrations", $"{Formats.HorseRegistrations(TimerStore.MaxHorseRegistrations)}.");
        return result;
    }

    public void ExportBackup(string destination, string appVersion) => BackupArchive.Export(_dataDir, destination,
        new(Settings.Current, Timers.Current, Todos.Current), Clock, appVersion);

    public PreparedRestore PrepareRestore(string archive) => BackupArchive.Prepare(archive, AppContext.BaseDirectory);

    public void RestartForRestore(PreparedRestore restore)
    {
        if (_mainViewModel is null) ((App)_app).RestartForRestore(restore);
        else _mainViewModel.RequestLeave(() => ((App)_app).RestartForRestore(restore), () => BackupArchive.Discard(restore));
    }

    public void NotifyRestore(string? previousDirectory)
    {
        Log.Info($"Backup restored; previous data: {previousDirectory ?? "none"}");
        _toast.ShowInfo("Backup restored", "Your previous data was kept beside the Data folder.");
    }

    public void Quit()
    {
        if (_mainViewModel is not null && !_mainViewModel.RequestLeave(QuitNow))
        {
            ShowMainWindow();
            return;
        }
        if (_mainViewModel is null) QuitNow();
    }

    void QuitNow()
    {
        IsQuitting = true;
        _app.Shutdown();
    }

    public void Dispose()
    {
        SystemEvents.TimeChanged -= OnTimeChanged;
        _mainViewModel?.Dispose();
        Agent.Dispose();
        Undo.Dispose();
        _theme.Dispose();
        _updateShutdown.Cancel();
        _updateHttp.Dispose();
        _updateShutdown.Dispose();
        UiClock.Stop();
        _loop.Dispose();
        Tts.Dispose();
        Overlay.Dispose();
        _tray.Dispose();
    }
}
