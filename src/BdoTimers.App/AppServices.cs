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
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;
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
    CancellationTokenSource? _preview;

    public TimerStore Timers { get; }
    public PersistentState<AppSettings> Settings { get; }
    public BossSeed Seed { get; }
    public UiClock UiClock { get; } = new();
    public IAlertSink Alerts { get; }
    public TtsChannel Tts { get; }
    public OverlayController Overlay { get; }
    public ArtLibrary Art { get; }
    public UserSounds Sounds { get; }
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
        RecoveredFiles = new[] { settings.RecoveredBackupPath, timers.RecoveredBackupPath }.OfType<string>().ToList();

        Settings = new PersistentState<AppSettings>(settingsFile, settings.Value);
        Timers = new TimerStore(timersFile, timers.Value);
        Seed = SeedService.LoadEmbedded();
        Timers.Update(d => SeedService.ApplyIfNeeded(DataMigrations.Apply(d, Settings.Current), Seed, new AlertConfig()));

        _toast = new ToastChannel();
        _toast.Activated += () => _app.Dispatcher.BeginInvoke(ShowMainWindow);
        Tts = new TtsChannel();
        Alerts = new AlertDispatcher(_toast, _sound, Tts, Settings, Sounds);
        _engine = new SchedulerEngine(Timers, Settings, Alerts, new SystemClock());
        _loop = new SchedulerLoop(_engine);
        _tray = new TrayIcon(this);
        Overlay = new OverlayController(this);
    }

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
        [new TimerDef { Name = "Test boss" }], DateTimeOffset.UtcNow.AddMinutes(5), 5, 5));

    /// <summary>The key that plays for a timer sound key; null means the app-wide alert sound.</summary>
    public string PlayableSound(string? key) => SoundKeys.Playable(key, Settings.Current.AlertSound, Sounds.Exists);

    /// <summary>
    /// Plays a sound once at the current volume for the ▶ buttons. A new preview stops the previous one, so repeated
    /// presses don't pile up. UI thread only.
    /// </summary>
    public async void PlaySound(string? key)
    {
        _preview?.Cancel();
        var preview = _preview = new CancellationTokenSource();
        try { await _sound.PlayAsync(PlayableSound(key), Settings.Current.Volume, preview.Token); }
        catch (Exception ex) { Log.Error("Sound preview failed", ex); }
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

    /// <summary>Deletes a user sound; timers and the app-wide sound that used it go back to their defaults.</summary>
    public bool RemoveSound(string key)
    {
        try { Sounds.Delete(key); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Most likely still playing; the file is released when playback ends.
            Log.Error($"Couldn't remove sound {key}", ex);
            return false;
        }
        Timers.ForgetSound(key);
        Settings.Update(s => s.AlertSound == key ? s with { AlertSound = BuiltInSounds.Default } : s);
        return true;
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(_dataDir);
        Process.Start(new ProcessStartInfo(_dataDir) { UseShellExecute = true });
    }

    public void ResetBossTimetable() => Timers.Update(d => SeedService.ResetBuiltIns(d, Seed, new AlertConfig()));

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
