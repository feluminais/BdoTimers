using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

/// <summary>
/// Brings timer data saved by older versions up to date; data that is already current comes back unchanged. Releases
/// save version 4 or later, so the steps start there.
/// </summary>
public static class DataMigrations
{
    public const int Current = 5;

    public static AppData Apply(AppData data) =>
        data.DataVersion >= Current ? data : data with { DataVersion = Current, Timers = SeparateHorseRegistration(data.Timers) };

    /// <summary>Version 5: keep an existing horse countdown as its own run when the preset becomes a launcher.</summary>
    static IReadOnlyList<TimerDef> SeparateHorseRegistration(IReadOnlyList<TimerDef> timers)
    {
        var template = timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration);
        if (template?.Countdown is not { Status: not CountdownStatus.Idle } countdown) return timers;
        var idle = timers.Select(t => t.Id == template.Id ? t with { Countdown = CountdownOps.Reset(countdown) } : t);
        return [.. idle, Presets.HorseRun(template, 1)];
    }
}
