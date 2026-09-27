using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class HotkeyRulesTests
{
    const int A = 0x41, Five = 0x35, Space = 0x20, Comma = 0xBC, Enter = 0x0D, F9 = 0x78, NumPad5 = 0x65, Home = 0x24;

    [Theory]
    [InlineData(A)]
    [InlineData(Five)]
    [InlineData(Space)]
    [InlineData(Comma)]
    [InlineData(Enter)]
    public void Keys_that_type_need_ctrl_or_alt(int key)
    {
        Assert.Equal(HotkeyRules.NeedsModifier, HotkeyRules.Check(new Hotkey(HotkeyModifiers.None, key), null));
        Assert.Equal(HotkeyRules.NeedsModifier, HotkeyRules.Check(new Hotkey(HotkeyModifiers.Shift, key), null));
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.Ctrl, key), null));
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Shift, key), null));
    }

    [Theory]
    [InlineData(F9)]
    [InlineData(NumPad5)]
    [InlineData(Home)]
    public void Keys_that_dont_type_may_stand_alone(int key) =>
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.None, key), null));

    [Fact]
    public void The_two_hotkeys_must_differ()
    {
        var combo = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x4F);
        Assert.Equal(HotkeyRules.SameAsOther, HotkeyRules.Check(combo, combo with { }));
        Assert.Null(HotkeyRules.Check(combo, new Hotkey(HotkeyModifiers.Ctrl, 0x4F)));
    }
}
