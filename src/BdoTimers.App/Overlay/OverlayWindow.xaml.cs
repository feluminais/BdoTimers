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
