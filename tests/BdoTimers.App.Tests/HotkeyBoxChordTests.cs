using System.Windows;
using System.Windows.Input;
using BdoTimers.App.Controls;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Tests;

public class HotkeyBoxChordTests
{
    static readonly Hotkey CtrlShift = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0);

    static void WithBox(bool chord, Action<HotkeyBox> test) => WpfTest.Run(() =>
    {
        var box = new HotkeyBox { Chord = chord };
        var window = new Window { Content = box, Width = 300, Height = 100, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            box.Focus();
            Assert.True(box.HandleKeyDown(Key.Enter, ModifierKeys.None));
            test(box);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void A_chord_field_takes_the_modifiers_held_together_once_the_last_is_let_go() => WithBox(true, box =>
    {
        Assert.True(box.HandleKeyDown(Key.LeftCtrl, ModifierKeys.Control));
        Assert.True(box.HandleKeyDown(Key.LeftShift, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.True(box.HandleKeyUp(Key.LeftShift));
        Assert.Null(box.Combo);
        Assert.True(box.HandleKeyUp(Key.LeftCtrl));
        Assert.Equal(CtrlShift, box.Combo);
        Assert.False(box.HandleKeyUp(Key.LeftCtrl));
        Assert.Equal("Ctrl+Shift", HotkeyText.Format(box.Combo!));
    });

    [Fact]
    public void A_chord_field_still_takes_a_combo_with_a_key_and_the_modifiers_let_go_after_it_change_nothing() => WithBox(true, box =>
    {
        Assert.True(box.HandleKeyDown(Key.LeftCtrl, ModifierKeys.Control));
        Assert.True(box.HandleKeyDown(Key.F9, ModifierKeys.Control));
        var combo = new Hotkey(HotkeyModifiers.Ctrl, KeyInterop.VirtualKeyFromKey(Key.F9));
        Assert.Equal(combo, box.Combo);
        box.HandleKeyUp(Key.LeftCtrl);
        Assert.Equal(combo, box.Combo);
    });

    [Fact]
    public void A_field_that_is_not_a_chord_waits_for_a_key() => WithBox(false, box =>
    {
        Assert.True(box.HandleKeyDown(Key.LeftCtrl, ModifierKeys.Control));
        Assert.True(box.HandleKeyUp(Key.LeftCtrl));
        Assert.Null(box.Combo);
        Assert.True(box.HandleKeyDown(Key.F9, ModifierKeys.Control));
        Assert.Equal(new Hotkey(HotkeyModifiers.Ctrl, KeyInterop.VirtualKeyFromKey(Key.F9)), box.Combo);
    });

    [Fact]
    public void Escape_cancels_a_chord_that_is_being_held() => WithBox(true, box =>
    {
        Assert.True(box.HandleKeyDown(Key.LeftCtrl, ModifierKeys.Control));
        Assert.True(box.HandleKeyDown(Key.Escape, ModifierKeys.None));
        Assert.False(box.HandleKeyUp(Key.LeftCtrl));
        Assert.Null(box.Combo);
    });

    [Fact]
    public void A_chord_another_hotkey_already_holds_is_refused() => WithBox(true, box =>
    {
        box.Taken = [CtrlShift];
        Assert.True(box.HandleKeyDown(Key.LeftCtrl, ModifierKeys.Control));
        Assert.True(box.HandleKeyDown(Key.LeftShift, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.True(box.HandleKeyUp(Key.LeftShift));
        Assert.True(box.HandleKeyUp(Key.LeftCtrl));
        Assert.Null(box.Combo);
    });
}
