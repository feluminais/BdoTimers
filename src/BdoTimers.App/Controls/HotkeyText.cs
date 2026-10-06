using System.Windows.Input;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Controls;

/// <summary>How a hotkey reads in its field: "Ctrl+Shift+O", "F9", "Num 5"; a held chord of modifiers alone, "Ctrl+Shift".</summary>
public static class HotkeyText
{
    public static string Format(Hotkey hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        if (hotkey.VirtualKey != 0) parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(hotkey.VirtualKey)));
        return string.Join("+", parts);
    }

    static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(int)(key - Key.NumPad0)}",
        Key.Multiply => "Num *",
        Key.Add => "Num +",
        Key.Subtract => "Num -",
        Key.Divide => "Num /",
        Key.Decimal => "Num .",
        Key.PageUp => "Page Up",
        Key.PageDown => "Page Down",
        Key.Return => "Enter",
        Key.Back => "Backspace",
        Key.Capital => "Caps Lock",
        Key.OemTilde => "`",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemPipe => "\\",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        _ => key.ToString(),
    };
}
