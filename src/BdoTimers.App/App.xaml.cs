using System.IO;
using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

public partial class App : Application
{
    /// <summary>Names the data folder and the single-instance handles. Debug builds get their own, so a dev copy
    /// runs next to the installed app without touching its timers and settings.</summary>
#if DEBUG
    const string InstanceName = "BdoTimers-Dev";
#else
    const string InstanceName = "BdoTimers";
#endif

    Mutex? _singleInstance;
    EventWaitHandle? _activateSignal;
    AppServices? _services;

    public bool IsQuittingApp => _services?.IsQuitting ?? true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), InstanceName);
        Log.Init(Path.Combine(dataDir, "logs"));
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

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleInstance?.Dispose();
        _activateSignal?.Dispose();
        base.OnExit(e);
    }
}
