using System.IO;
using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

public partial class App : Application
{
    Mutex? _singleInstance;
    EventWaitHandle? _activateSignal;
    AppServices? _services;

    public bool IsQuittingApp => _services?.IsQuitting ?? true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\BdoTimers.SingleInstance", out var isFirst);
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BdoTimers.Activate");
        if (!isFirst)
        {
            _activateSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_activateSignal,
            (_, _) => Dispatcher.BeginInvoke(() => _services?.ShowMainWindow()), null, Timeout.Infinite, false);

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BdoTimers");
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

        _services = new AppServices(this, dataDir);
        _services.Start(showWindow: !e.Args.Contains("--minimized"));
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
