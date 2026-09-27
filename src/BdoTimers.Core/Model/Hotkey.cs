namespace BdoTimers.Core.Model;

/// <summary>Modifier keys, with the values both Windows' RegisterHotKey and WPF's ModifierKeys use.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>A global key combo; <see cref="VirtualKey"/> is a Windows virtual-key code, so Core needs no UI types.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int VirtualKey);
