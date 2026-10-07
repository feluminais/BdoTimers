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
    CancellationTokenSource? _preview;
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
        Settings.Changed += () =>
        {
            try { Todos.Reconcile(Settings.Current); }
            catch (StateSaveException ex) { Log.Error("Couldn't save to-do reset settings", ex); Health.Failed("Saving", ex); }
        };
        Timers = new TimerStore(timersFile, timers.Value);
        Timers.Changed += () => Health.Saved(timersFile.FilePath);
        Timers.Update(d =>
        {
            var migrated = DataMigrations.Apply(d, Settings.Current);
            return Presets.Ensure(SeedService.ApplyIfNeeded(migrated, _seeds[migrated.SelectedBossRegion], new AlertConfig()));
        });
        // The Garmoth tracker's week follows its switch and the weekly reset, which both change in Settings.
        Settings.Changed += () =>
        {
            try { Timers.ReconcileGarmoth(Settings.Current, Clock.UtcNow); }
            catch (StateSaveException ex) { Log.Error("Couldn't save the Garmoth tracker", ex); Health.Failed("Saving", ex); }
        };
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
        _engine = new SchedulerEngine(Timers, Settings, _alerts, Clock);
        _loop = new SchedulerLoop(_engine, Clock,
            onFailure: ex => Health.Failed(ex is StateSaveException ? "Saving" : "Scheduler", ex),
            onRecovered: _ => Health.Succeeded("Scheduler"));
        _tray = new TrayIcon(this);
        Overlay = new OverlayController(this);

        var updateFile = new JsonFileStore<UpdateCheckState>(Path.Combine(dataDir, "update-check.json"), () => new());
        UpdateCheckState updateState;
        try { updateState = updateFile.Load().Value; }
        catch (Exception ex)
        {
            Log.Error("Couldn't load update check timing", ex);
            updateState = new();
        }
        Updates = new UpdateChecker(new GitHubReleaseSource(_updateHttp), ProductVersion.Number, Clock,
            new PersistentState<UpdateCheckState>(updateFile, updateState), App.AutomaticUpdateChecks,
            shutdown: _updateShutdown.Token);
    }

    /// <summary>Optional steps log their failures and carry on, so none of them can stop the app from starting.</summary>
    public void Start(bool showWindow, bool measuring = false)
    {
        try { _engine.ReconcileStartup(); }
        catch (Exception ex) { Log.Error("Couldn't complete countdowns that ended while closed", ex); Health.Failed("Startup", ex); }
        try { Todos.Reconcile(Settings.Current); }
        catch (StateSaveException ex) { Log.Error("Couldn't save to-do lists", ex); Health.Failed("Saving", ex); }
        try { Timers.ReconcileGarmoth(Settings.Current, Clock.UtcNow); }
        catch (StateSaveException ex) { Log.Error("Couldn't save the Garmoth tracker", ex); Health.Failed("Saving", ex); }
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
        try { Autostart.Apply(Settings.Current.Autostart); }
        catch (Exception ex) { Log.Error("Couldn't update autostart", ex); }
        NotifyTimetableReview();
        if (showWindow) ShowMainWindow();
        else EcoQos.Set(true);
#if !DEBUG
        _ = Task.Run(CheckForUpdatesOnStartup);
#endif
    }

    static void OnTimeChanged(object? sender, EventArgs e) => TimeZoneInfo.ClearCachedData();

    void NotifyTimetableReview()
    {
        var data = Timers.Current;
        var seed = _seeds[data.SelectedBossRegion];
        var timetableRevision = TimetableUpdates.Revision(seed);
        if (TimetableUpdates.Review(data, seed).NeedsReview
            && BossRegions.State(data).TimetableNoticeRevision != timetableRevision
            && _toast.ShowInfo($"Review the {BossRegions.Find(data.SelectedBossRegion).ShortLabel} timetable",
                "Bundled spawn times can be reviewed in Settings → Bosses."))
        {
            try
            {
                Timers.Update(d => BossRegions.WithState(d,
                    BossRegions.State(d, data.SelectedBossRegion) with { TimetableNoticeRevision = timetableRevision }));
            }
            catch (Exception ex) { Log.Error("Couldn't save that the timetable notice was shown", ex); }
        }
    }

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

    /// <summary>Brings the window up with the Overlay panel open.</summary>
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

    public void SendTestAlert() => SendTestAlert(Settings.Current);

    public async void SendTestAlert(AppSettings previewSettings)
    {
        if (Health.TestBusy) return;
        Health.TestBusy = true;
        Health.TestStatus = "Sending…";
        try
        {
            await _alerts.SendTestAsync(new AlertEvent([new TimerDef { Name = "Test boss" }], Clock.UtcNow.AddMinutes(5), 5, 5), previewSettings);
            Health.TestStatus = Health.Status is null ? "Test sent" : "Some channels failed. See Diagnostics.";
        }
        catch (Exception ex)
        {
            Log.Error("Test alert failed", ex);
            Health.Failed("App", ex);
            Health.TestStatus = "Couldn't send the test. See Diagnostics.";
        }
        finally { Health.TestBusy = false; }
    }

    /// <summary>The key that plays for a timer sound key; null means the app-wide alert sound.</summary>
    public string PlayableSound(string? key) => SoundKeys.Playable(key, Settings.Current.AlertSound, Sounds.Exists);

    /// <summary>
    /// Plays a sound once at the current volume for the ▶ buttons. A new preview stops the previous one, so repeated
    /// presses don't pile up. UI thread only.
    /// </summary>
    public void PlaySound(string? key) => Preview("Sound", cancel => _sound.PlayAsync(PlayableSound(key), Settings.Current.Volume, cancel));
    public void PlaySound(string? key, float volume) => Preview("Sound", cancel => _sound.PlayAsync(PlayableSound(key), volume, cancel));

    /// <summary>Speaks <paramref name="text"/> with the chosen voice and speed, like an alert would.</summary>
    public void Speak(string text) => Speak(text, Settings.Current);
    public void Speak(string text, AppSettings s) => Preview("Voice", async cancel =>
    {
        var speech = await Tts.SynthesizeAsync(text, s.TtsVoice, s.TtsRate);
        if (cancel.IsCancellationRequested) speech.Dispose();
        else await _sound.PlayAsync(speech, s.Volume, cancel);
    });

    async void Preview(string area, Func<CancellationToken, Task> play)
    {
        _preview?.Cancel();
        var preview = _preview = new CancellationTokenSource();
        try { await play(preview.Token); if (!preview.IsCancellationRequested) Health.Succeeded(area); }
        catch (OperationCanceledException) when (preview.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!preview.IsCancellationRequested)
            {
                Log.Error("Preview failed", ex);
                Health.Failed(area, ex);
                MessageBox.Show(Health.LastFailure?.Message, "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (_preview == preview) _preview = null;
            preview.Dispose();
        }
    }

    /// <summary>
    /// Asks for a picture and copies it into the pictures folder. File is null when cancelled or refused; a copy that
    /// fails or a file that isn't a readable picture gives the error to show.
    /// </summary>
    public (string? File, string? Error) ChoosePicture()
    {
        var dialog = new OpenFileDialog { Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
        if (dialog.ShowDialog() != true) return (null, null);
        var error = $"Couldn't add {Path.GetFileName(dialog.FileName)}.";
        string file;
        try { file = Art.Import(dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Couldn't import picture {dialog.FileName}", ex);
            return (null, error);
        }
        if (Art.UserPicture(file) is not null) return (file, null);
        Art.Delete(file);
        return (null, error);
    }

    /// <summary>Asks for a WAV or MP3 and copies it into the user's sounds. Key is null when cancelled or refused.</summary>
    public (string? Key, string? Error) AddSound()
    {
        var dialog = new OpenFileDialog { Filter = "Sounds (WAV, MP3)|*.wav;*.mp3", Title = "Add a sound" };
        if (dialog.ShowDialog() != true) return (null, null);
        if (!SoundChannel.CanDecode(dialog.FileName)) return (null, $"Couldn't play {Path.GetFileName(dialog.FileName)}.");
        try { return (Sounds.Import(dialog.FileName), null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Error($"Couldn't add sound {dialog.FileName}", ex);
            return (null, $"Couldn't add {Path.GetFileName(dialog.FileName)}.");
        }
    }

    /// <summary>
    /// Deletes a user sound; timers and the app-wide sound that used it go back to their defaults. Returns the error
    /// to show, or null once it's gone.
    /// </summary>
    public string? RemoveSound(string key)
    {
        try { Sounds.Delete(key); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Most likely still playing; the file is released when playback ends.
            Log.Error($"Couldn't remove sound {key}", ex);
            return $"Couldn't remove {Path.GetFileNameWithoutExtension(key)}. Try again in a moment.";
        }
        Timers.ForgetSound(key);
        Settings.Update(s => s.AlertSound == key ? s with { AlertSound = BuiltInSounds.Default } : s);
        return null;
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(_dataDir);
        Process.Start(new ProcessStartInfo(_dataDir) { UseShellExecute = true });
    }

    public void OpenLogs()
    {
        try
        {
            var directory = Path.Combine(_dataDir, "logs");
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            Health.Succeeded("Diagnostics");
            Health.DiagnosticStatus = null;
        }
        catch (Exception ex) { Log.Error("Couldn't open logs", ex); Health.Failed("Diagnostics", ex); }
    }

    public async void CopyDiagnostics()
    {
        if (Health.DiagnosticsBusy) return;
        Health.DiagnosticsBusy = true;
        Health.DiagnosticStatus = "Measuring…";
        try
        {
            var report = await PerformanceMetrics.ReportAsync(Health.LastFailure, _updateShutdown.Token);
            Clipboard.SetText(report);
            Health.Succeeded("Diagnostics");
            Health.DiagnosticStatus = "Copied";
        }
        catch (OperationCanceledException) when (_updateShutdown.IsCancellationRequested) { }
        catch (Exception ex) { Log.Error("Couldn't copy diagnostics", ex); Health.Failed("Diagnostics", ex); Health.DiagnosticStatus = null; }
        finally { Health.DiagnosticsBusy = false; }
    }

    public void ExportBackup(string destination, string appVersion) => BackupArchive.Export(_dataDir, destination,
        new(Settings.Current, Timers.Current, Todos.Current), Clock, appVersion);

    public PreparedRestore PrepareRestore(string archive) => BackupArchive.Prepare(archive, AppContext.BaseDirectory);

    public void RestartForRestore(PreparedRestore restore)
    {
        if (_mainViewModel is null) ((App)_app).RestartForRestore(restore);
        else _mainViewModel.RequestLeave(() => ((App)_app).RestartForRestore(restore), () => BackupArchive.Discard(restore));
    }

    public void SelectBossRegion(string regionId)
    {
        Timers.SelectBossRegion(regionId, Clock);
        NotifyTimetableReview();
    }

    public void ApplyTimetable(IEnumerable<string> names) => ReleasingDroppedPictures(() =>
        Timers.Update(data => TimetableUpdates.Apply(data, _seeds[data.SelectedBossRegion], names)));

    public void NotifyRestore(string? previousDirectory)
    {
        Log.Info($"Backup restored; previous data: {previousDirectory ?? "none"}");
        _toast.ShowInfo("Backup restored", "Your previous data was kept beside the Data folder.");
    }

    /// <summary>Brings back the selected region's bundled bosses and spawn times; bosses the player added stay.</summary>
    public void ResetBossTimetable() => ReleasingDroppedPictures(() =>
        Timers.Update(d => SeedService.ResetBuiltIns(d, _seeds[d.SelectedBossRegion], new AlertConfig())));

    /// <summary>Deletes the pictures of bosses that <paramref name="change"/> removed from the timetable.</summary>
    void ReleasingDroppedPictures(Action change)
    {
        var before = Timers.Current.Timers.Select(t => t.ImageFile).OfType<string>().ToList();
        change();
        foreach (var image in before) Undo.ReleasePicture(image);
    }

    /// <summary>The spawn times this version ships for a boss, or null when its region's timetable no longer has it.</summary>
    public ScheduledSpec? BundledSchedule(TimerDef boss) =>
        _seeds.TryGetValue(BossRegions.RegionOf(boss), out var seed) ? SeedService.Schedule(seed, boss.Name) : null;

    public void SetTodoReset(TodoCadence cadence, TodoSchedule schedule)
    {
        var previous = Settings.Current;
        var changed = cadence == TodoCadence.Daily
            ? previous with { DailyTodoReset = schedule }
            : previous with { WeeklyTodoReset = schedule };
        if (changed == previous) return;
        // Save list boundaries first: if that fails, Settings keeps the old reset.
        Todos.Reconcile(changed);
        try { Settings.Update(s => cadence == TodoCadence.Daily
            ? s with { DailyTodoReset = schedule }
            : s with { WeeklyTodoReset = schedule }); }
        catch (StateSaveException)
        {
            try { Todos.Reconcile(previous); }
            catch (StateSaveException ex) { Log.Error("Couldn't restore to-do reset after settings save failed", ex); }
            throw;
        }
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
