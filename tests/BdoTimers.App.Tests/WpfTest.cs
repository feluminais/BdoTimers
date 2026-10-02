using System.Runtime.ExceptionServices;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BdoTimers.App.Tests;

internal static class WpfTest
{
    static readonly Lazy<Dispatcher> Ui = new(() =>
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var name = new StringBuilder(256);
                if (!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, name, name.Capacity * 2, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!name.ToString().StartsWith("BdoTimers.Tests.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Run Windows UI tests through scripts/test-windows.ps1 to keep test focus on an undisplayed desktop.");
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/BdoTimers;component/Theme/AppResources.xaml", UriKind.Relative)
                });
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex) { error = ex; }
            finally { ready.Set(); }
            if (dispatcher is not null) Dispatcher.Run();
        }) { IsBackground = true, Name = "WPF smoke tests" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("WPF test dispatcher did not start");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        return dispatcher!;
    });

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern IntPtr GetThreadDesktop(uint threadId);

    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out int needed);

    public static void Run(Action action) => Ui.Value.Invoke(action);


    public static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
