using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace BdoTimers.App.Overlay;

public partial class OverlayWindow : Window
{
    bool _clickThrough = true;

    public OverlayWindow(OverlayViewModel model)
    {
        InitializeComponent();
        DataContext = Model = model;
        SourceInitialized += (_, _) => ApplyStyles();
        SizeChanged += (_, _) => KeepOnScreen();
        MouseLeftButtonDown += (_, e) =>
        {
            if (_clickThrough || e.ButtonState != MouseButtonState.Pressed) return;
            DragMove();
            Dropped?.Invoke();
        };
    }

    public OverlayViewModel Model { get; }

    /// <summary>Raised when a drag ends.</summary>
    public event Action? Dropped;

    /// <summary>Layouts and scale can grow past the screen edge; keep the measured window on its monitor.</summary>
    void KeepOnScreen()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || ActualWidth <= 0 || ActualHeight <= 0) return;
        var monitor = NativeMethods.MonitorFromWindow(handle, 2); // MONITOR_DEFAULTTONEAREST
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget is not { } target) return;
        var fromDevice = target.TransformFromDevice;
        var start = fromDevice.Transform(new Point(info.Work.Left, info.Work.Top));
        var end = fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom));
        Left = Math.Clamp(Left, start.X, Math.Max(start.X, end.X - ActualWidth));
        Top = Math.Clamp(Top, start.Y, Math.Max(start.Y, end.Y - ActualHeight));
    }

    /// <summary>
    /// A borderless game window can climb above other topmost windows. Raises the overlay again only when another app's
    /// window that overlaps it sits above it, so the z-order over the game isn't changed on every tick.
    /// </summary>
    public void KeepOnTop()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var bounds)) return;
        // Bounded, since the z-order can change during the walk.
        var above = NativeMethods.GetWindow(handle, NativeMethods.GW_HWNDPREV);
        for (var i = 0; i < 256 && above != IntPtr.Zero; i++, above = NativeMethods.GetWindow(above, NativeMethods.GW_HWNDPREV))
        {
            if (!Covers(above, bounds)) continue;
            NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
            return;
        }
    }

    /// <summary>A shown, uncloaked window that overlaps <paramref name="bounds"/>. The app's own menus and tooltips may
    /// stay above the overlay.</summary>
    static bool Covers(IntPtr window, NativeMethods.Rect bounds)
    {
        if (!NativeMethods.IsWindowVisible(window)) return false;
        NativeMethods.GetWindowThreadProcessId(window, out var process);
        if (process == (uint)Environment.ProcessId) return false;
        if (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0
            && cloaked != 0) return false;
        return NativeMethods.GetWindowRect(window, out var r)
               && r.Left < bounds.Right && bounds.Left < r.Right && r.Top < bounds.Bottom && bounds.Top < r.Bottom;
    }

    /// <summary>Click-through lets mouse input reach the game; the Overlay panel's preview turns it off so the
    /// overlay can be dragged.</summary>
    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        ApplyStyles();
    }

    void ApplyStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE)
                    | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        style = _clickThrough ? style | NativeMethods.WS_EX_TRANSPARENT : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, style);
    }
}
