using System.Threading;
using System.Windows;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

/// <summary>Entry point the bundle engine talks to: shows the setup window, or runs silently for /quiet and /passive.</summary>
internal sealed class SetupApplication : BootstrapperApplication
{
    IEngine _engine = null!;
    IBootstrapperCommand _command = null!;

    protected override void OnCreate(CreateEventArgs args)
    {
        base.OnCreate(args);
        _engine = args.Engine;
        _command = args.Command;
    }

    protected override void Run()
    {
        var flow = new SetupFlow(this, _engine, _command);
        var exitCode = _command.Display == Display.Full ? RunWindow(flow) : flow.RunQuiet();
        _engine.Quit(exitCode);
    }

    /// <summary>WPF needs its own STA thread; this method blocks until the window closes.</summary>
    static int RunWindow(SetupFlow flow)
    {
        var exitCode = 0;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/SetupUi;component/Theme/Theme.xaml"),
            });
            var viewModel = new SetupViewModel(flow);
            var window = new MainWindow(viewModel);
            flow.Detect();
            app.Run(window);
            exitCode = viewModel.ExitCode;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }
}
