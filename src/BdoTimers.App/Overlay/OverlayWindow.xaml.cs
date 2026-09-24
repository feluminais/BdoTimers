using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BdoTimers.App.Overlay;

public sealed record OverlayRow(string Name, string Countdown);

public partial class OverlayWindow : Window
{
    bool _clickThrough = true;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyStyles();
        MouseLeftButtonDown += (_, e) =>
        {
            if (!_clickThrough && e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
    }

    public void SetRows(IReadOnlyList<OverlayRow> rows) => Items.ItemsSource = rows;

    /// <summary>Click-through lets mouse input reach the game; positioning mode turns it off so the window can be dragged.</summary>
    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        Frame.BorderBrush = (Brush)FindResource(enabled ? "AccentSoftBrush" : "AccentTextBrush");
        Frame.BorderThickness = new Thickness(enabled ? 1 : 2);
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
