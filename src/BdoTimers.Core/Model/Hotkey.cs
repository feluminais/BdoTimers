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

/// <summary>Global shortcuts chosen away from BDO's unmodified function keys and common combat inputs.</summary>
public static class DefaultHotkeys
{
    const HotkeyModifiers Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Shift;
    const int F8 = 0x77, F9 = 0x78, F10 = 0x79;

    public static readonly Hotkey AlwaysShow = new(Modifiers, F8);
    public static readonly Hotkey Show = new(Modifiers, F9);
    public static readonly Hotkey HorseRegistration = new(Modifiers, F10);
}

public static class HotkeyRules
{
    public const string NeedsModifier = "Add Ctrl or Alt";
    public const string UsedByAnother = "Used by another hotkey";

    const HotkeyModifiers NonTyping = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win;

    /// <summary>
    /// Why <paramref name="hotkey"/> can't be used beside the <paramref name="taken"/> ones, or null when it can. Windows
    /// keeps a hotkey from the game, so a key that types in chat needs Ctrl or Alt; Shift+letter still types.
    /// </summary>
    public static string? Check(Hotkey hotkey, Hotkey? other) => Check(hotkey, other, null);

    public static string? Check(Hotkey hotkey, Hotkey? other, Hotkey? another)
        => CheckAgainst(hotkey, new[] { other, another }.OfType<Hotkey>());

    public static string? Check(Hotkey hotkey, IEnumerable<Hotkey?> taken) => CheckAgainst(hotkey, taken.OfType<Hotkey>());

    public static string? CheckAgainst(Hotkey hotkey, IEnumerable<Hotkey> others)
    {
        if (TypesText(hotkey.VirtualKey) && (hotkey.Modifiers & NonTyping) == 0) return NeedsModifier;
        return others.Contains(hotkey) ? UsedByAnother : null;
    }

    /// <summary>Backspace, Tab, Enter, Space, 0-9, A-Z and the punctuation keys.</summary>
    static bool TypesText(int vk) =>
        vk is 0x08 or 0x09 or 0x0D or 0x20 or (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A)
            or (>= 0xBA and <= 0xC0) or (>= 0xDB and <= 0xDF) or 0xE2;
}
