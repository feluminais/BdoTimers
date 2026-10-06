using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class HotkeyChordTests
{
    const int Shift = 0x10, Control = 0x11, Menu = 0x12, LeftWin = 0x5B, RightWin = 0x5C, F9 = 0x78, A = 0x41;

    static Func<int, bool> Down(params int[] keys) => keys.Contains;

    [Fact]
    public void Modifiers_alone_are_held_while_each_of_them_is_down()
    {
        var chord = DefaultHotkeys.Move;
        Assert.True(HotkeyChord.IsHeld(chord, Down(Control, Shift)));
        Assert.False(HotkeyChord.IsHeld(chord, Down(Control)));
        Assert.False(HotkeyChord.IsHeld(chord, Down(Shift)));
        Assert.False(HotkeyChord.IsHeld(chord, Down()));
    }

    [Fact]
    public void Keys_held_with_the_chord_do_not_count_against_it()
    {
        Assert.True(HotkeyChord.IsHeld(DefaultHotkeys.Move, Down(Control, Shift, Menu, A, F9)));
    }

    [Fact]
    public void A_chord_with_a_key_needs_that_key_as_well()
    {
        var chord = new Hotkey(HotkeyModifiers.Ctrl, F9);
        Assert.False(HotkeyChord.IsHeld(chord, Down(Control)));
        Assert.False(HotkeyChord.IsHeld(chord, Down(F9)));
        Assert.True(HotkeyChord.IsHeld(chord, Down(Control, F9)));
    }

    [Fact]
    public void Either_windows_key_counts_and_alt_is_the_menu_key()
    {
        var chord = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0);
        Assert.True(HotkeyChord.IsHeld(chord, Down(LeftWin, Menu)));
        Assert.True(HotkeyChord.IsHeld(chord, Down(RightWin, Menu)));
        Assert.False(HotkeyChord.IsHeld(chord, Down(Menu)));
    }

    [Fact]
    public void A_combo_of_nothing_is_never_held()
    {
        Assert.False(HotkeyChord.IsHeld(new Hotkey(HotkeyModifiers.None, 0), _ => true));
    }

    [Fact]
    public void The_default_is_Ctrl_and_Shift_alone_and_clashes_with_no_other_default()
    {
        Assert.Equal(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0), DefaultHotkeys.Move);
        var others = new[] { DefaultHotkeys.AlwaysShow, DefaultHotkeys.Show, DefaultHotkeys.HorseRegistration };
        Assert.Null(HotkeyRules.CheckAgainst(DefaultHotkeys.Move, others));
        Assert.Equal(DefaultHotkeys.Move, new OverlaySettings().MoveHotkey);
    }

    [Fact]
    public void The_chord_is_saved_cleared_and_defaulted_for_files_that_predate_it()
    {
        Assert.Equal(DefaultHotkeys.Move, JsonSerializer.Deserialize<OverlaySettings>("{}", JsonDefaults.Options)!.MoveHotkey);
        var custom = new OverlaySettings { MoveHotkey = new Hotkey(HotkeyModifiers.Alt, 0) };
        Assert.Equal(custom.MoveHotkey, JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(custom, JsonDefaults.Options), JsonDefaults.Options)!.MoveHotkey);
        var cleared = new OverlaySettings { MoveHotkey = null };
        Assert.Null(JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(cleared, JsonDefaults.Options), JsonDefaults.Options)!.MoveHotkey);
    }

    [Fact]
    public void The_chord_reserves_its_combo_but_is_never_registered_with_windows()
    {
        var data = new AppData();
        var overlay = new OverlaySettings();
        var move = new HotkeyTarget(HotkeyAction.MoveOverlay);
        Assert.DoesNotContain(move, HotkeyCatalog.Active(data, overlay).Keys);
        Assert.Contains(DefaultHotkeys.Move, HotkeyCatalog.OtherKeys(data, overlay, new(HotkeyAction.AlwaysShow)));
        var others = HotkeyCatalog.OtherKeys(data, overlay, move);
        Assert.DoesNotContain(DefaultHotkeys.Move, others);
        Assert.Contains(DefaultHotkeys.AlwaysShow, others);
        Assert.Equal(DefaultHotkeys.AlwaysShow, HotkeyCatalog.Active(data, overlay)[new(HotkeyAction.AlwaysShow)]);
    }

    [Fact]
    public void A_chord_set_to_another_hotkeys_combo_clashes_with_it()
    {
        var overlay = new OverlaySettings { MoveHotkey = DefaultHotkeys.Show };
        var data = new AppData();
        Assert.Equal(HotkeyRules.UsedByAnother, HotkeyRules.CheckAgainst(DefaultHotkeys.Show, HotkeyCatalog.OtherKeys(data, overlay, new(HotkeyAction.MoveOverlay))));
        Assert.DoesNotContain(new HotkeyTarget(HotkeyAction.Show), HotkeyCatalog.Active(data, overlay).Keys);
    }
}
