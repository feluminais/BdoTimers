using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Overlay;

public partial class OverlayWindow : Window
{
    bool _clickThrough = true;
    HwndSource? _source;
    bool _screenCheckPending;
    readonly DispatcherTimer _mouseCheck = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(75) };
    readonly Func<IntPtr, (WindowRect Bounds, double X, double Y)?> _readPointer;
    readonly Func<Hotkey, bool> _isHeld;
    readonly DispatcherTimer _moveCheck = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(75) };
    Hotkey? _moveHotkey;
    bool _moving;
    OverlayMouseProximity _mouseProximity;
    OverlayMouseAvoidance _mouseAvoidance;
    double _targetOpacity = 1;
    bool _mouseErrorLogged;

    public OverlayWindow(OverlayViewModel model) : this(model, ReadPointer) { }

    internal OverlayWindow(OverlayViewModel model, Func<IntPtr, (WindowRect Bounds, double X, double Y)?> readPointer,
        Func<Hotkey, bool>? isHeld = null)
    {
        _readPointer = readPointer;
        _isHeld = isHeld ?? HeldNow;
        InitializeComponent();
        DataContext = Model = model;
        _mouseCheck.Tick += OnMouseCheck;
        _moveCheck.Tick += (_, _) => CheckMoveKeys();
        IsVisibleChanged += (_, _) =>
        {
            UpdateMouseTracking();
            UpdateMoveTracking();
        };
        SourceInitialized += (_, _) =>
        {
            ApplyStyles();
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(ScreenMessages);
        };
        SizeChanged += (_, _) =>
        {
            KeepOnScreen();
            if (_mouseCheck.IsEnabled) CheckMouse();
        };
        DpiChanged += (_, _) => QueueScreenCheck();
        MouseLeftButtonDown += (_, e) =>
        {
            if ((_clickThrough && !_moving) || e.ButtonState != MouseButtonState.Pressed) return;
            DragMove();
            Dropped?.Invoke();
        };
    }

    public OverlayViewModel Model { get; }

    /// <summary>Raised when a drag ends.</summary>
    public event Action? Dropped;

    IntPtr ScreenMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x007E or 0x001A) QueueScreenCheck(); // Display or work-area changes.
        return IntPtr.Zero;
    }

    void QueueScreenCheck()
    {
        if (_screenCheckPending) return;
        _screenCheckPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _screenCheckPending = false;
            if (_source is not null) KeepOnScreen();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _mouseCheck.Stop();
        _moveCheck.Stop();
        _mouseCheck.Tick -= OnMouseCheck;
        _source?.RemoveHook(ScreenMessages);
        _source = null;
        base.OnClosed(e);
    }

    /// <summary>Layouts and scale can grow past the screen edge; keep the measured window on its monitor.</summary>
    void KeepOnScreen()
    {
        var bounds = new WindowRect(Left, Top, ActualWidth, ActualHeight);
        if (new WindowInteropHelper(this).Handle == IntPtr.Zero || !bounds.IsValid) return;
        var placed = WindowGeometry.Clamp(bounds, VirtualScreen.WorkAreas(this));
        Left = placed.Left;
        Top = placed.Top;
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
        UpdateMouseTracking();
        UpdateMoveTracking();
    }

    /// <summary>
    /// While these keys are held, whichever window has the keyboard, the overlay takes the mouse and can be dragged where it
    /// shows. Mouse proximity goes on working meanwhile; null turns this off.
    /// </summary>
    public void SetMoveHotkey(Hotkey? hotkey)
    {
        if (_moveHotkey == hotkey) return;
        _moveHotkey = hotkey;
        UpdateMoveTracking();
    }

    /// <summary>The Overlay panel's preview is draggable already, so the keys are only read while the overlay shows and clicks pass through it.</summary>
    void UpdateMoveTracking()
    {
        if (IsVisible && _clickThrough && _moveHotkey is not null)
        {
            CheckMoveKeys();
            _moveCheck.Start();
        }
        else
        {
            _moveCheck.Stop();
            SetMoving(false);
        }
    }

    void CheckMoveKeys() => SetMoving(_moveHotkey is { } keys && _isHeld(keys));

    void SetMoving(bool moving)
    {
        if (_moving == moving) return;
        _moving = moving;
        Model.IsMoveMode = moving;
        Cursor = moving ? Cursors.SizeAll : null;
        ApplyStyles();
    }

    static bool HeldNow(Hotkey chord) =>
        HotkeyChord.IsHeld(chord, key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);

    public void SetMouseProximity(OverlayMouseProximity mode)
    {
        _mouseProximity = mode;
        UpdateMouseTracking();
    }

    void UpdateMouseTracking()
    {
        if (IsVisible && _clickThrough && _mouseProximity is OverlayMouseProximity.Fade or OverlayMouseProximity.Hide)
        {
            CheckMouse();
            _mouseCheck.Start();
        }
        else
        {
            _mouseCheck.Stop();
            _mouseAvoidance = new();
            SetProximityOpacity(1, immediate: true);
        }
    }

    void OnMouseCheck(object? sender, EventArgs e) => CheckMouse();

    void CheckMouse()
    {
        try
        {
            var sample = _readPointer(new WindowInteropHelper(this).Handle);
            // Cursor reads can fail on a locked desktop. Restore rather than leave an invisible overlay stuck.
            _mouseAvoidance = sample is { } p ? _mouseAvoidance.Update(_mouseProximity, p.Bounds, p.X, p.Y) : new();
            SetProximityOpacity(_mouseAvoidance.Opacity(_mouseProximity));
            _mouseErrorLogged = false;
        }
        catch (Exception ex)
        {
            _mouseAvoidance = new();
            SetProximityOpacity(1, immediate: true);
            if (!_mouseErrorLogged) Log.Error("Overlay mouse proximity failed", ex);
            _mouseErrorLogged = true;
        }
    }

    void SetProximityOpacity(double target, bool immediate = false)
    {
        if (immediate)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = _targetOpacity = target;
            return;
        }
        if (_targetOpacity == target) return;
        var from = Opacity;
        Opacity = _targetOpacity = target;
        BeginAnimation(OpacityProperty, new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(150))
        {
            FillBehavior = FillBehavior.Stop,
        });
    }

    static (WindowRect Bounds, double X, double Y)? ReadPointer(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var rect)
            || !NativeMethods.GetCursorPos(out var point)) return null;
        // Both native readings use screen pixels. Convert together so the margins stay consistent across DPI changes.
        var dpi = NativeMethods.GetDpiForWindow(handle);
        var scale = dpi == 0 ? 1 : dpi / 96.0;
        return (new WindowRect(rect.Left / scale, rect.Top / scale,
            (rect.Right - rect.Left) / scale, (rect.Bottom - rect.Top) / scale), point.X / scale, point.Y / scale);
    }

    void ApplyStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE)
                    | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        style = _clickThrough && !_moving ? style | NativeMethods.WS_EX_TRANSPARENT : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, style);
    }
}
