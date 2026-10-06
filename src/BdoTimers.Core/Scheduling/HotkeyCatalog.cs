using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

/// <summary>All configured shortcuts reserve their combo, including shortcuts whose feature is switched off.</summary>
public static class HotkeyCatalog
{
    static Dictionary<HotkeyTarget, Hotkey> Configured(AppData data, OverlaySettings overlay)
    {
        var keys = new Dictionary<HotkeyTarget, Hotkey>();
        if (overlay.AlwaysShowHotkey is { } always) keys[new(HotkeyAction.AlwaysShow)] = always;
        if (overlay.ShowHotkey is { } show) keys[new(HotkeyAction.Show)] = show;
        if (overlay.MoveHotkey is { } move) keys[new(HotkeyAction.MoveOverlay)] = move;
        if (data.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration)?.StartHotkey is { } horse)
            keys[new(HotkeyAction.StartHorseRegistration)] = horse;
        foreach (var timer in data.Timers.Where(CustomCountdowns.Includes))
            if (timer.ControlHotkey is { } key) keys[new(HotkeyAction.ControlCountdown, timer.Id)] = key;
        return keys;
    }

    public static IReadOnlyList<Hotkey> OtherKeys(AppData data, OverlaySettings overlay, HotkeyTarget target) =>
        Configured(data, overlay).Where(pair => pair.Key != target).Select(pair => pair.Value).ToList();

    /// <summary>The combos Windows is asked to hold. The move chord is not one: it is read while the overlay shows.</summary>
    public static IReadOnlyDictionary<HotkeyTarget, Hotkey> Active(AppData data, OverlaySettings overlay)
    {
        var configured = Configured(data, overlay);
        return configured.Where(pair => pair.Key.Action switch
            {
                HotkeyAction.AlwaysShow => overlay.Enabled,
                HotkeyAction.Show => overlay.Enabled && overlay.ShowOnHotkey,
                HotkeyAction.MoveOverlay => false,
                _ => true,
            })
            .Where(pair => HotkeyRules.CheckAgainst(pair.Value,
                configured.Where(other => other.Key != pair.Key).Select(other => other.Value)) is null)
            .ToDictionary();
    }
}
