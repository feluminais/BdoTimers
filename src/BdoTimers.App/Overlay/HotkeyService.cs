using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.Overlay;

/// <summary>
/// Holds global shortcuts with RegisterHotKey, which receives input while an administrator game has focus.
/// Windows keeps the combo from the game. UI thread only.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int MOD_NOREPEAT = 0x4000;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    readonly HwndSource _window = new(0, 0, 0, 0, 0, "BdoTimers hotkeys", HWND_MESSAGE);
    readonly HotkeyRegistry _registry;

    public event Action<HotkeyTarget>? Pressed;
    public event Action? RefusedChanged;
    public IReadOnlySet<HotkeyTarget> Refused => _registry.Refused;

    public HotkeyService()
    {
        _registry = new HotkeyRegistry(new Registration(_window.Handle));
        _registry.RefusedChanged += () => RefusedChanged?.Invoke();
        _window.AddHook(WndProc);
    }

    public void Set(IReadOnlyDictionary<HotkeyTarget, Hotkey> bindings) => _registry.Set(bindings);
    public void Suspend() => _registry.Suspend();
    public void Resume() => _registry.Resume();

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _registry.TargetFor(wParam.ToInt32()) is { } target)
        {
            handled = true;
            Pressed?.Invoke(target);
        }
        return IntPtr.Zero;
    }

    sealed class Registration(IntPtr window) : IHotkeyRegistration
    {
        public bool Register(int id, Hotkey key)
        {
            if (NativeMethods.RegisterHotKey(window, id, (int)key.Modifiers | MOD_NOREPEAT, key.VirtualKey)) return true;
            Log.Info($"Windows refused hotkey {key}: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            return false;
        }

        public void Unregister(int id)
        {
            NativeMethods.UnregisterHotKey(window, id);
        }
    }

    public void Dispose()
    {
        _registry.Dispose();
        _window.Dispose();
    }
}
