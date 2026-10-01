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

public static class HotkeyRules
{
    public const string NeedsModifier = "Add Ctrl or Alt";
    public const string UsedByAnother = "Used by another hotkey";

    const HotkeyModifiers NonTyping = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win;

    /// <summary>
    /// Why <paramref name="hotkey"/> can't be used beside the <paramref name="taken"/> ones, or null when it can. Windows
    /// keeps a hotkey from the game, so a key that types in chat needs Ctrl or Alt; Shift+letter still types.
    /// </summary>
    public static string? Check(Hotkey hotkey, IEnumerable<Hotkey?> taken)
    {
        if (TypesText(hotkey.VirtualKey) && (hotkey.Modifiers & NonTyping) == 0) return NeedsModifier;
        return taken.Contains(hotkey) ? UsedByAnother : null;
    }

    /// <summary>Backspace, Tab, Enter, Space, 0-9, A-Z and the punctuation keys.</summary>
    static bool TypesText(int vk) =>
        vk is 0x08 or 0x09 or 0x0D or 0x20 or (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A)
            or (>= 0xBA and <= 0xC0) or (>= 0xDB and <= 0xDF) or 0xE2;
}
