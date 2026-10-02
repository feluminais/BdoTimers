using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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
    readonly IReadOnlyDictionary<string, BossSeed> _seeds;
    public UiClock UiClock { get; } = new();
    /// <summary>The boss board shared by the overlay and the Bosses screen.</summary>
    public BossBoardCache Boards { get; } = new();
    public AlertDispatcher Alerts { get; }
    public IClock Clock { get; } = new SystemClock();
    public TtsChannel Tts { get; }
    public OverlayController Overlay { get; }
    public ArtLibrary Art { get; }
    public UserSounds Sounds { get; }
    public UpdateChecker Updates { get; }
    public bool IsQuitting { get; private set; }
    IReadOnlyList<string> RecoveredFiles { get; }

    public AppServices(Application app, string dataDir)
    {
        _app = app;
        _dataDir = dataDir;
        Art = new ArtLibrary(Path.Combine(dataDir, "images"));
        Sounds = new UserSounds(Path.Combine(dataDir, "sounds"));
        _sound = new SoundChannel(Sounds);
        var settingsFile = new JsonFileStore<AppSettings>(Path.Combine(dataDir, "settings.json"), () => new AppSettings());
        var timersFile = new JsonFileStore<AppData>(Path.Combine(dataDir, "timers.json"), () => new AppData());
        var settings = settingsFile.Load();
        var timers = timersFile.Load();

        Settings = new PersistentState<AppSettings>(settingsFile, settings.Value);
        var todosFile = new JsonFileStore<TodoData>(Path.Combine(dataDir, "todos.json"),
            () => TodoSeed.Create(Clock.UtcNow, Settings.Current));
        var todos = todosFile.Load();
        if (!File.Exists(todosFile.FilePath)) todosFile.Save(todos.Value);
        RecoveredFiles = new[] { settings.RecoveredBackupPath, timers.RecoveredBackupPath, todos.RecoveredBackupPath }
            .OfType<string>().ToList();
        Todos = new TodoStore(todosFile, todos.Value, Clock);
        Todos.Update(TodoMigrations.Apply);
        Settings.Changed += () =>
        {
            try { Todos.ApplyDefaultSchedules(Settings.Current); }
            catch (StateSaveException ex) { Log.Error("Couldn't save to-do reset settings", ex); }
        };
        // A built-in sound from an earlier version that the app no longer has.
        if (!SoundKeys.IsKnown(Settings.Current.AlertSound)) Settings.Update(s => s with { AlertSound = BuiltInSounds.Default });
        Timers = new TimerStore(timersFile, timers.Value);
        _seeds = BossRegions.All.ToDictionary(r => r.Id, r => SeedService.LoadEmbedded(r.Id));
        Timers.Update(d =>
        {
            var migrated = DataMigrations.Apply(d, Settings.Current);
            return Presets.Ensure(SeedService.ApplyIfNeeded(migrated, _seeds[migrated.SelectedBossRegion], new AlertConfig()));
        });

        _toast = new ToastChannel(App.InstanceName, App.DisplayName);
        _toast.Activated += () => _app.Dispatcher.BeginInvoke(ShowMainWindow);
        Tts = new TtsChannel(new KokoroEngine(Path.Combine(AppContext.BaseDirectory, "Voice", "kokoro")),
            new SpeechCache(Path.Combine(dataDir, "speech")));
        Tts.CleanCacheInBackground();
        Alerts = new AlertDispatcher(_toast, _sound, Tts, Settings, Sounds, Timers);
        _engine = new SchedulerEngine(Timers, Settings, Alerts, Clock);
        _loop = new SchedulerLoop(_engine, Clock);
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
    public void Start(bool showWindow)
    {
        try { _engine.ReconcileStartup(); }
        catch (Exception ex) { Log.Error("Couldn't complete countdowns that ended while closed", ex); }
        try
        {
            Todos.ApplyDefaultSchedules(Settings.Current);
            Todos.Reconcile();
        }
        catch (StateSaveException ex) { Log.Error("Couldn't save to-do lists", ex); }
        var todoResetErrors = new RepeatingErrorLog("To-do reset", Clock);
        UiClock.Tick += _ =>
        {
            try
            {
                Todos.Reconcile();
                todoResetErrors.Succeeded();
            }
            catch (StateSaveException ex) { todoResetErrors.Failed(ex); }
        };
        _loop.Start();
        UiClock.Start();
        Overlay.Start();
        foreach (var path in RecoveredFiles)
        {
            try
            {
                _toast.ShowInfo("A data file was damaged",
                    $"BDO Timers started with defaults. The damaged file was kept as {Path.GetFileName(path)}.");
            }
            catch (Exception ex) { Log.Error($"Damaged file notice failed for {path}", ex); }
        }
        if (!Settings.Current.NotificationHintShown)
        {
            // Marked as shown only once it was, so a failure brings it back at the next start.
            try
            {
                _toast.ShowInfo("Let alerts through while gaming",
                    "Add BDO Timers to Settings → Notifications → Set priority notifications.",
                    withNotificationSettingsButton: true);
                Settings.Update(s => s with { NotificationHintShown = true });
            }
            catch (Exception ex) { Log.Error("Notification hint failed", ex); }
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

    void NotifyTimetableReview()
    {
        var data = Timers.Current;
        var seed = _seeds[data.SelectedBossRegion];
        var timetableRevision = TimetableUpdates.Revision(seed);
        if (TimetableUpdates.Review(data, seed).NeedsReview && BossRegions.State(data).TimetableNoticeRevision != timetableRevision)
        {
            try
            {
                _toast.ShowInfo($"Review the {BossRegions.Find(data.SelectedBossRegion).ShortLabel} timetable", "Bundled spawn times can be reviewed in Settings → Bosses.");
                Timers.Update(d => BossRegions.WithState(d, BossRegions.State(d, data.SelectedBossRegion) with { TimetableNoticeRevision = timetableRevision }));
            }
            catch (Exception ex) { Log.Error("Timetable notice failed", ex); }
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
        if (_main is null)
        {
            _mainViewModel = new MainViewModel(this);
            _main = new MainWindow(_mainViewModel, Settings);
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void PauseAlerts(TimeSpan? duration) =>
        Settings.Update(s => AlertPause.Pause(s, DateTimeOffset.UtcNow, duration));

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
        if (result == HorseStartResult.Started && announce) Alerts.Say("Horse registration time started");
        else if (result == HorseStartResult.LimitReached)
        {
            try { _toast.ShowInfo("Horse registrations", "Maximum 10 running."); }
            catch (Exception ex) { Log.Error("Horse registration limit notice failed", ex); }
        }
        return result;
    }

    public void SendTestAlert() => Alerts.Dispatch(new AlertEvent(
        [new TimerDef { Name = "Test boss" }], DateTimeOffset.UtcNow.AddMinutes(5), 5, 5));

    /// <summary>The key that plays for a timer sound key; null means the app-wide alert sound.</summary>
    public string PlayableSound(string? key) => SoundKeys.Playable(key, Settings.Current.AlertSound, Sounds.Exists);

    /// <summary>
    /// Plays a sound once at the current volume for the ▶ buttons. A new preview stops the previous one, so repeated
    /// presses don't pile up. UI thread only.
    /// </summary>
    public void PlaySound(string? key) => Preview(cancel => _sound.PlayAsync(PlayableSound(key), Settings.Current.Volume, cancel));

    /// <summary>Speaks <paramref name="text"/> with the chosen voice and speed, like an alert would.</summary>
    public void Speak(string text) => Preview(async cancel =>
    {
        var s = Settings.Current;
        var speech = await Tts.SynthesizeAsync(text, s.TtsVoice, s.TtsRate);
        if (cancel.IsCancellationRequested) speech.Dispose();
        else await _sound.PlayAsync(speech, s.Volume, cancel);
    });

    async void Preview(Func<CancellationToken, Task> play)
    {
        _preview?.Cancel();
        var preview = _preview = new CancellationTokenSource();
        try { await play(preview.Token); }
        catch (Exception ex) { Log.Error("Preview failed", ex); }
        finally
        {
            if (_preview == preview) _preview = null;
            preview.Dispose();
        }
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

    public void ExportBackup(string destination, string appVersion) => BackupArchive.Export(_dataDir, destination,
        new(Settings.Current, Timers.Current, Todos.Current), Clock, appVersion);

    public PreparedRestore PrepareRestore(string archive) => BackupArchive.Prepare(archive, AppContext.BaseDirectory);

    public void RestartForRestore(PreparedRestore restore) => ((App)_app).RestartForRestore(restore);

    public void SelectBossRegion(string regionId)
    {
        Timers.SelectBossRegion(regionId, Clock);
        NotifyTimetableReview();
    }

    public void ApplyTimetable(IEnumerable<string> names) =>
        Timers.Update(data => TimetableUpdates.Apply(data, _seeds[data.SelectedBossRegion], names));

    public void NotifyRestore(string? previousDirectory)
    {
        Log.Info($"Backup restored; previous data: {previousDirectory ?? "none"}");
        try { _toast.ShowInfo("Backup restored", "Your previous data was kept beside the Data folder."); }
        catch (Exception ex) { Log.Error("Couldn't show the restore notice", ex); }
    }

    public void ResetBossTimetable() => Timers.Update(d => SeedService.ResetBuiltIns(d, _seeds[d.SelectedBossRegion], new AlertConfig()));

    public void SetTodoReset(TodoCadence cadence, TodoSchedule schedule)
    {
        var previous = Settings.Current;
        var changed = cadence == TodoCadence.Daily
            ? previous with { DailyTodoReset = schedule }
            : previous with { WeeklyTodoReset = schedule };
        if (changed == previous) return;
        // Save list boundaries first: if that fails, Settings keeps the old reset.
        Todos.ApplyDefaultSchedules(changed);
        try { Settings.Update(s => cadence == TodoCadence.Daily
            ? s with { DailyTodoReset = schedule }
            : s with { WeeklyTodoReset = schedule }); }
        catch (StateSaveException)
        {
            try { Todos.ApplyDefaultSchedules(previous); }
            catch (StateSaveException ex) { Log.Error("Couldn't restore to-do reset after settings save failed", ex); }
            throw;
        }
    }

    public void Quit()
    {
        IsQuitting = true;
        _app.Shutdown();
    }

    public void Dispose()
    {
        _mainViewModel?.Dispose();
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
