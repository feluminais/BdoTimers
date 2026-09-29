using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Sounds;

namespace BdoTimers.Core.Storage;

/// <summary>Brings timer data saved by older versions up to date; data that is already current comes back unchanged.</summary>
public static class DataMigrations
{
    public const int Current = 4;

    /// <summary>Lists that earlier versions gave timers themselves: the built-in default, and 5 and 0 for countdowns.</summary>
    static readonly IReadOnlyList<IReadOnlyList<int>> AssignedLeadTimes = [AlertConfig.StandardLeadTimesMinutes, [5, 0]];

    public static AppData Apply(AppData data, AppSettings settings)
    {
        if (data.DataVersion >= Current) return data;
        var timers = data.Timers;
        if (data.DataVersion < 2) timers = FollowDefaultLeadTimes(timers, settings);
        if (data.DataVersion < 3) timers = ForgetRetiredSounds(timers);
        if (data.DataVersion < 4) timers = AddHorseRegistration(timers);
        return data with { DataVersion = Current, Timers = timers };
    }

    /// <summary>
    /// Version 2: alert times follow the default in Settings unless a timer has its own. Earlier versions copied a list
    /// into every timer, so a timer still holding the current default, or a list the app assigned, follows it.
    /// </summary>
    static IReadOnlyList<TimerDef> FollowDefaultLeadTimes(IReadOnlyList<TimerDef> timers, AppSettings settings)
    {
        var followed = AssignedLeadTimes.Append(settings.DefaultLeadTimesMinutes).Select(l => l.ToHashSet()).ToList();
        return timers
            .Select(t => t.Alerts.LeadTimesMinutes is { } own && followed.Any(f => f.SetEquals(own))
                ? t with { Alerts = t.Alerts with { LeadTimesMinutes = null } }
                : t)
            .ToList();
    }

    /// <summary>Version 3: the first built-in sounds were replaced; timers that picked one go back to Default.</summary>
    static IReadOnlyList<TimerDef> ForgetRetiredSounds(IReadOnlyList<TimerDef> timers) =>
        timers
            .Select(t => t.Alerts.Sound.Key is { } key && !SoundKeys.IsKnown(key)
                ? t with { Alerts = t.Alerts with { Sound = t.Alerts.Sound with { Key = null } } }
                : t)
            .ToList();

    /// <summary>
    /// Version 4: the Horse registration preset. It can be deleted, so it is added here, once, rather than at every
    /// startup like Farm and Fishing.
    /// </summary>
    static IReadOnlyList<TimerDef> AddHorseRegistration(IReadOnlyList<TimerDef> timers) =>
        [.. timers, Presets.CreateHorseRegistration()];
}
