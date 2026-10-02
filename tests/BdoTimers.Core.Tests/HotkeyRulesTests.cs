using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class HotkeyRulesTests
{
    const int A = 0x41, Five = 0x35, Space = 0x20, Comma = 0xBC, Enter = 0x0D, F9 = 0x78, NumPad5 = 0x65, Home = 0x24;

    [Fact]
    public void Default_hotkeys_are_distinct_and_accepted()
    {
        var keys = new[] { DefaultHotkeys.AlwaysShow, DefaultHotkeys.Show, DefaultHotkeys.HorseRegistration };
        Assert.Equal(3, keys.Distinct().Count());
        foreach (var key in keys)
            Assert.Null(HotkeyRules.Check(key, keys.First(other => other != key)));
    }

    [Theory]
    [InlineData(A)]
    [InlineData(Five)]
    [InlineData(Space)]
    [InlineData(Comma)]
    [InlineData(Enter)]
    public void Keys_that_type_need_ctrl_or_alt(int key)
    {
        Assert.Equal(HotkeyRules.NeedsModifier, HotkeyRules.Check(new Hotkey(HotkeyModifiers.None, key), []));
        Assert.Equal(HotkeyRules.NeedsModifier, HotkeyRules.Check(new Hotkey(HotkeyModifiers.Shift, key), []));
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.Ctrl, key), []));
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Shift, key), []));
    }

    [Theory]
    [InlineData(F9)]
    [InlineData(NumPad5)]
    [InlineData(Home)]
    public void Keys_that_dont_type_may_stand_alone(int key) =>
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyModifiers.None, key), []));

    [Fact]
    public void A_hotkey_must_differ_from_every_taken_one()
    {
        var combo = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x4F);
        var other = new Hotkey(HotkeyModifiers.Ctrl, 0x4F);
        Assert.Equal(HotkeyRules.UsedByAnother, HotkeyRules.Check(combo, [other, combo with { }]));
        Assert.Null(HotkeyRules.Check(combo, [other, null]));
    }
}
