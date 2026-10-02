using System.Diagnostics;
using System.IO;
using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

public partial class App : Application
{
    /// <summary>Names the single-instance handles and the notification app id. Debug builds get their own, so a dev
    /// copy runs next to the installed app without taking over its window or notifications.</summary>
#if DEBUG
    internal const string InstanceName = "BdoTimers-Dev";
    internal const string DisplayName = "BDO Timers (dev)";
    internal const bool AutomaticUpdateChecks = false;
#else
    /// <remarks>The setup window removes the notification registration by this name (<see cref="Alerts.NotificationRegistration.InstalledAppId"/>).</remarks>
    internal const string InstanceName = "BdoTimers";
    internal const string DisplayName = "BDO Timers";
    internal const bool AutomaticUpdateChecks = true;
#endif

    Mutex? _singleInstance;
    EventWaitHandle? _activateSignal;
    AppServices? _services;
    PreparedRestore? _restartRestore;
    bool _restartRequested;
    bool _handlingFailure;

    internal void RestartForRestore(PreparedRestore restore)
    {
        _restartRestore = restore;
        _services?.Quit();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Initialize before services so startup includes loading saved state and starting the tray.
        PerformanceMetrics.Begin();

        if (Argument(e.Args, "--wait-for-exit") is { } processId && int.TryParse(processId, out var previousId))
        {
            try
            {
                if (previousId == Environment.ProcessId) throw new InvalidOperationException("Invalid restart request.");
                using var previous = Process.GetProcessById(previousId);
                if (!previous.WaitForExit(30000)) throw new IOException("The previous app is still closing.");
            }
            catch (ArgumentException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
        }

        var performanceReport = Argument(e.Args, "--performance-report");
        var instance = performanceReport is null ? InstanceName : InstanceName + ".Performance";
        _singleInstance = new Mutex(true, $@"Local\{instance}.SingleInstance", out var isFirst);
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{instance}.Activate");
        if (!isFirst)
        {
            _activateSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_activateSignal,
            (_, _) => Dispatcher.BeginInvoke(() => _services?.ShowMainWindow()), null, Timeout.Infinite, false);

        // Data stays beside the exe. Relocating an installation requires copying Data or restoring a backup.
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!CanWrite(dataDir))
        {
            MessageBox.Show($"BDO Timers can't save to {dataDir}.\n\nInstall in a writable folder, then copy your existing Data folder into the new BdoTimers folder.",
                "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }
        string? restoredPrevious = null;
        var restoring = Argument(e.Args, "--restore-prepared");
        if (restoring is not null)
        {
            try { restoredPrevious = BackupArchive.ApplyPrepared(restoring, dataDir, new SystemClock()); }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't restore the backup: {ex.Message}", "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
        }
        Log.Init(Path.Combine(dataDir, "logs"), new SystemClock());
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true;
            if (args.Exception is StateSaveException save)
            {
                _services?.Health.Failed("Saving", save);
                MessageBox.Show($"{save.Message}\n\nThe change was not applied.", "BDO Timers",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_handlingFailure) { Shutdown(1); return; }
            _handlingFailure = true;
            _services?.Health.Failed("App", args.Exception);
            _restartRequested = MessageBox.Show("An unexpected error occurred. BDO Timers needs to close.\n\nRestart now?",
                "BDO Timers", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        // A half-started app has no window or tray icon yet still holds the single-instance mutex,
        // so every later launch would silently hand off to it. Fail visibly and exit instead.
        try
        {
            _services = new AppServices(this, dataDir);
            _services.Start(showWindow: performanceReport is null && !e.Args.Contains("--minimized") && !e.Args.Contains("--behind"),
                measuring: performanceReport is not null);
#if DEBUG
            if (e.Args.Contains("--behind")) _services.ShowMainWindowBehind();
#endif
            if (restoring is not null) _services.NotifyRestore(restoredPrevious);
        }
        catch (UnsupportedDataVersionException ex)
        {
            Log.Error("Saved data needs a newer version", ex);
            MessageBox.Show(ex.Message, "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            MessageBox.Show($"BDO Timers couldn't start: {ex.Message}\n\nDetails are in {Path.Combine(dataDir, "logs")}.",
                "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        Log.Info("Started");
        PerformanceMetrics.Started();
        if (performanceReport is not null)
        {
            var settle = int.TryParse(Argument(e.Args, "--settle-seconds"), out var seconds) ? Math.Clamp(seconds, 1, 60) : 30;
            MeasureAndExit(performanceReport, settle, e.Args.Contains("--skip-idle-unload") ? 0 : 190);
        }
    }

    async void MeasureAndExit(string destination, int settleSeconds, int unloadSeconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settleSeconds));
            var idle = await PerformanceMetrics.ReportAsync(_services!.Health.LastFailure);
            try
            {
                var settings = _services.Settings.Current;
                using var speech = await _services.Tts.SynthesizeAsync("Performance check", settings.TtsVoice, settings.TtsRate);
            }
            catch (Exception ex) { Log.Error("Performance speech check failed", ex); _services.Health.Failed("Voice", ex); }
            var voice = await PerformanceMetrics.ReportAsync(_services.Health.LastFailure);
            var report = $"Idle before speech\n{idle}\n\nAfter first speech\n{voice}\n";
            if (unloadSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(unloadSeconds));
                report += $"\nAfter voice idle timeout\n{await PerformanceMetrics.ReportAsync(_services.Health.LastFailure)}\n";
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            await File.WriteAllTextAsync(destination, report);
            _services.Quit();
        }
        catch (Exception ex) { Log.Error("Performance measurement failed", ex); Shutdown(1); }
    }

    static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Creates the folder if needed and tries a throwaway file in it.</summary>
    static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            using (File.Create(Path.Combine(dir, $".write-test-{Guid.NewGuid():N}.tmp"), 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleInstance?.Dispose();
        _activateSignal?.Dispose();
        base.OnExit(e);
        if (_restartRestore is not null || _restartRequested)
        {
            try
            {
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "BdoTimers.exe")) { UseShellExecute = false };
                start.ArgumentList.Add("--wait-for-exit");
                start.ArgumentList.Add(Environment.ProcessId.ToString());
                if (_restartRestore is { } restore)
                {
                    start.ArgumentList.Add("--restore-prepared");
                    start.ArgumentList.Add(restore.Directory);
                }
                Process.Start(start);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't restart: {ex.Message}\nOpen BDO Timers again.",
                    "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
