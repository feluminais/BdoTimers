using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class HotkeyCatalogTests
{
    static TimerDef Custom(Hotkey key) => new()
    {
        Kind = TimerKind.Countdown, Countdown = new CountdownSpec(), ControlHotkey = key, Enabled = false,
    };
    static readonly Hotkey Key = new(HotkeyModifiers.Ctrl, 0x47);

    [Fact]
    public void Conflicts_include_both_overlay_keys_horse_and_every_other_custom_timer_even_when_disabled()
    {
        var self = Custom(Key);
        var others = Enumerable.Range(0, 4).Select(i => Custom(new Hotkey(HotkeyModifiers.Alt, 0x41 + i))).ToList();
        var horse = Presets.CreateHorseRegistration();
        var data = new AppData { Timers = [self, horse, .. others] };
        var overlay = new OverlaySettings { Enabled = false, ShowOnHotkey = false };
        var target = new HotkeyTarget(HotkeyAction.ControlCountdown, self.Id);
        var conflicts = HotkeyCatalog.OtherKeys(data, overlay, target);

        foreach (var key in new[] { overlay.AlwaysShowHotkey!, overlay.ShowHotkey!, horse.StartHotkey! }
            .Concat(others.Select(t => t.ControlHotkey!)))
            Assert.Equal(HotkeyRules.UsedByAnother, HotkeyRules.CheckAgainst(key, conflicts));
        Assert.Null(HotkeyRules.CheckAgainst(Key, conflicts));

        Assert.Contains(Key, HotkeyCatalog.OtherKeys(data, overlay, new(HotkeyAction.AlwaysShow)));
        Assert.Contains(Key, HotkeyCatalog.OtherKeys(data, overlay, new(HotkeyAction.Show)));
        Assert.Contains(Key, HotkeyCatalog.OtherKeys(data, overlay, new(HotkeyAction.StartHorseRegistration)));
    }

    [Fact]
    public void Custom_binding_is_independent_of_alerts_and_overlay_switches_and_releases_on_clear_or_delete()
    {
        var timer = Custom(Key);
        var horse = Presets.CreateHorseRegistration();
        var overlay = new OverlaySettings { Enabled = false };
        var data = new AppData { Timers = [timer, horse] };
        var target = new HotkeyTarget(HotkeyAction.ControlCountdown, timer.Id);
        var bindings = HotkeyCatalog.Active(data, overlay);
        Assert.Equal(Key, bindings[target]);
        Assert.Equal(horse.StartHotkey, bindings[new(HotkeyAction.StartHorseRegistration)]);
        Assert.Equal(2, bindings.Count);
        Assert.DoesNotContain(target, HotkeyCatalog.Active(data with { Timers = [timer with { ControlHotkey = null }, horse] }, overlay).Keys);
        Assert.DoesNotContain(target, HotkeyCatalog.Active(data with { Timers = [horse] }, overlay).Keys);
    }

    [Fact]
    public void Invalid_or_conflicting_saved_bindings_are_not_registered()
    {
        var overlay = new OverlaySettings();
        var data = new AppData
        {
            Timers = [Custom(overlay.AlwaysShowHotkey!), Custom(new Hotkey(HotkeyModifiers.None, 0x41))],
        };
        var bindings = HotkeyCatalog.Active(data, overlay);
        Assert.Single(bindings);
        Assert.Contains(new HotkeyTarget(HotkeyAction.Show), bindings.Keys);
    }
}
