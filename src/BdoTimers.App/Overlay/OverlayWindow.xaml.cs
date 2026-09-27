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
