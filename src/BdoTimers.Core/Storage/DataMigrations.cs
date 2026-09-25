using BdoTimers.Core.Model;

namespace BdoTimers.Core.Storage;

/// <summary>Brings timer data saved by older versions up to date; data that is already current comes back unchanged.</summary>
public static class DataMigrations
{
    public const int Current = 2;

    /// <summary>Lists that earlier versions gave timers themselves: the built-in default, and 5 and 0 for countdowns.</summary>
    static readonly IReadOnlyList<IReadOnlyList<int>> AssignedLeadTimes = [AlertConfig.StandardLeadTimesMinutes, [5, 0]];

    public static AppData Apply(AppData data, AppSettings settings)
    {
        if (data.DataVersion >= Current) return data;
        // Version 2: alert times follow the default in Settings unless a timer has its own. Earlier versions copied a
        // list into every timer, so a timer still holding the current default, or a list the app assigned, follows it.
        var followed = AssignedLeadTimes.Append(settings.DefaultLeadTimesMinutes).Select(l => l.ToHashSet()).ToList();
        return data with
        {
            DataVersion = Current,
            Timers = data.Timers
                .Select(t => t.Alerts.LeadTimesMinutes is { } own && followed.Any(f => f.SetEquals(own))
                    ? t with { Alerts = t.Alerts with { LeadTimesMinutes = null } }
                    : t)
                .ToList(),
        };
    }
}
