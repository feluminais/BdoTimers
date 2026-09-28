using System.Windows.Interop;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Overlay;

public enum HotkeyAction { AlwaysShow = 1, Show = 2 }

/// <summary>
/// Holds the overlay's hotkeys with Windows' RegisterHotKey, which keeps working while a game running as administrator
/// has focus (a low-level keyboard hook gets no input then). Windows keeps the combo from the game. UI thread only.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int MOD_NOREPEAT = 0x4000;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    // A message-only window: it only receives the hotkey messages, never broadcasts, and isn't a top-level window.
    readonly HwndSource _window = new(0, 0, 0, 0, 0, "BdoTimers hotkeys", HWND_MESSAGE);
    readonly Dictionary<HotkeyAction, Hotkey> _wanted = [];
    readonly HashSet<HotkeyAction> _held = [];
    HashSet<HotkeyAction> _refused = [];
    int _suspended;

    public event Action<HotkeyAction>? Pressed;
    /// <summary>Raised when the hotkeys Windows refused change.</summary>
    public event Action? RefusedChanged;

    public HotkeyService() => _window.AddHook(WndProc);

    public IReadOnlySet<HotkeyAction> Refused => _refused;

    /// <summary>Holds exactly these hotkeys; null releases one.</summary>
    public void Set(Hotkey? alwaysShow, Hotkey? show)
    {
        var wanted = new Dictionary<HotkeyAction, Hotkey>();
        if (alwaysShow is not null) wanted[HotkeyAction.AlwaysShow] = alwaysShow;
        if (show is not null) wanted[HotkeyAction.Show] = show;
        if (wanted.Count == _wanted.Count && wanted.All(w => _wanted.TryGetValue(w.Key, out var k) && k == w.Value)) return;
        _wanted.Clear();
        foreach (var (action, key) in wanted) _wanted[action] = key;
        Apply();
    }

    /// <summary>Releases every hotkey while a hotkey field listens, so the combo reaches the field.</summary>
    public void Suspend()
    {
        _suspended++;
        Apply();
    }

    public void Resume()
    {
        _suspended = Math.Max(0, _suspended - 1);
        Apply();
    }

    void Apply()
    {
        foreach (var action in _held) NativeMethods.UnregisterHotKey(_window.Handle, (int)action);
        _held.Clear();
        if (_suspended > 0) return;
        var refused = new HashSet<HotkeyAction>();
        foreach (var (action, key) in _wanted)
        {
            if (NativeMethods.RegisterHotKey(_window.Handle, (int)action, (int)key.Modifiers | MOD_NOREPEAT, key.VirtualKey))
                _held.Add(action);
            else
            {
                refused.Add(action);
                Log.Info($"Windows refused the {action} hotkey {key}");
            }
        }
        if (refused.SetEquals(_refused)) return;
        _refused = refused;
        RefusedChanged?.Invoke();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && Enum.IsDefined((HotkeyAction)wParam.ToInt32()))
        {
            handled = true;
            Pressed?.Invoke((HotkeyAction)wParam.ToInt32());
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var action in _held) NativeMethods.UnregisterHotKey(_window.Handle, (int)action);
        _held.Clear();
        _window.Dispose();
    }
}
