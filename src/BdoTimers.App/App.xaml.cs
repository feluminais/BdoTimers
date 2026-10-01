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
#else
    /// <remarks>The setup window removes the notification registration by this name (<see cref="Alerts.NotificationRegistration.InstalledAppId"/>).</remarks>
    internal const string InstanceName = "BdoTimers";
    internal const string DisplayName = "BDO Timers";
#endif

    Mutex? _singleInstance;
    EventWaitHandle? _activateSignal;
    AppServices? _services;
    PreparedRestore? _restartRestore;

    public bool IsQuittingApp => _services?.IsQuitting ?? true;

    public void Quit() => _services?.Quit();

    internal void RestartForRestore(PreparedRestore restore)
    {
        _restartRestore = restore;
        _services?.Quit();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        _singleInstance = new Mutex(true, $@"Local\{InstanceName}.SingleInstance", out var isFirst);
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{InstanceName}.Activate");
        if (!isFirst)
        {
            _activateSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_activateSignal,
            (_, _) => Dispatcher.BeginInvoke(() => _services?.ShowMainWindow()), null, Timeout.Infinite, false);

        // Beside the exe, so everything the app keeps is in the folder it was installed to. Setup carries this folder
        // along when the app moves; uninstall keeps it unless the player opts in to deletion.
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!CanWrite(dataDir))
        {
            MessageBox.Show($"BDO Timers can't save to {dataDir}.\n\nRun BDO Timers setup and move it to a folder you can write to.",
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
                MessageBox.Show($"{save.Message}\n\nThe change was not applied.", "BDO Timers",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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
            _services.Start(showWindow: !e.Args.Contains("--minimized"));
            if (restoring is not null) _services.NotifyRestore(restoredPrevious);
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
        if (_restartRestore is { } restore)
        {
            try
            {
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "BdoTimers.exe")) { UseShellExecute = false };
                start.ArgumentList.Add("--wait-for-exit");
                start.ArgumentList.Add(Environment.ProcessId.ToString());
                start.ArgumentList.Add("--restore-prepared");
                start.ArgumentList.Add(restore.Directory);
                Process.Start(start);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't restart: {ex.Message}\nOpen BDO Timers and choose the backup again.",
                    "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
