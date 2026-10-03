using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

public sealed record UpcomingItem(TimerDef Timer, DateTimeOffset AtUtc);

public static class UpcomingQuery
{
    /// <summary>How far ahead <see cref="OverlayStart"/> looks: a week and a day, so weekly times are always found.</summary>
    static readonly TimeSpan Reach = TimeSpan.FromDays(8);

    /// <summary>Unmuted occurrences of timers with an overlay pop-up inside each one's "show N minutes before" window.</summary>
    public static IReadOnlyList<UpcomingItem> ForOverlay(AppData data, OverlaySettings settings, DateTimeOffset now)
    {
        var muted = data.Muted.ToHashSet();
        return PopUpTimers(data, settings)
            .SelectMany(p => OccurrenceSource.Between(p.Timer, now, now + p.Window)
                .Where(at => !muted.Contains(new MutedOccurrence(p.Timer.Id, at)) && NewWindow(data, p.Timer, at, p.Window))
                .Select(at => new UpcomingItem(p.Timer, at)))
            .OrderBy(i => i.AtUtc)
            .ThenBy(i => BossOrder.Priority(i.Timer))
            .ThenBy(i => i.Timer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The earliest instant from <paramref name="now"/> on at which <see cref="ForOverlay"/> can return anything while
    /// the data and settings stay the same; at or before <paramref name="now"/> when a pop-up may be due now, null when no
    /// timer has overlay pop-ups. A timer with nothing in <see cref="Reach"/> counts as starting when the end of it enters
    /// its window.
    /// </summary>
    public static DateTimeOffset? OverlayStart(AppData data, OverlaySettings settings, DateTimeOffset now)
    {
        var muted = data.Muted.ToHashSet();
        DateTimeOffset? earliest = null;
        foreach (var (t, window) in PopUpTimers(data, settings))
        {
            if (t.Kind == TimerKind.OneTime || t.Scheduled is { StartDate: not null } or { EndDate: not null })
            {
                var next = OccurrenceSource.From(t, now)
                    .Where(at => !muted.Contains(new MutedOccurrence(t.Id, at)) && NewWindow(data, t, at, window))
                    .Cast<DateTimeOffset?>().FirstOrDefault();
                if (next is null) continue;
                var boundedStart = next.Value - window;
                if (earliest is null || boundedStart < earliest) earliest = boundedStart;
                continue;
            }
            var first = OccurrenceSource.Between(t, now, now + Reach)
                .Where(at => !muted.Contains(new MutedOccurrence(t.Id, at)))
                .Where(at => NewWindow(data, t, at, window))
                .Cast<DateTimeOffset?>()
                .FirstOrDefault();
            var start = (first ?? now + Reach) - window;
            if (earliest is null || start < earliest) earliest = start;
        }
        return earliest;
    }

    /// <summary>Timers with alerts on and an overlay pop-up, with how long before an occurrence it shows. The Guild
    /// bosses timer follows the Overlay panel's setting; other timers have their own.</summary>
    static IEnumerable<(TimerDef Timer, TimeSpan Window)> PopUpTimers(AppData data, OverlaySettings settings) =>
        data.Timers
            .Where(t => BossRegions.IsEligible(data, t) && t.Enabled)
            .Select(t => (Timer: t, PopUp: t.Preset == Presets.GuildBosses ? settings.GuildBosses : t.Alerts.Overlay))
            .Where(p => p.PopUp.Enabled)
            .Select(p => (p.Timer, TimeSpan.FromMinutes(p.PopUp.ShowMinutesBefore)));

    static bool NewWindow(AppData data, TimerDef timer, DateTimeOffset at, TimeSpan window) =>
        !AlertPlanner.SuppressedAfterRegionSwitch(timer, at - window, data.BossAlertsAfterUtc);
}

/// <summary>
/// Whether an overlay pop-up can be due, worked out once per data and settings instance and kept until the earliest one
/// can start, so a tick with nothing else to show the overlay for can skip building it. Not thread-safe.
/// </summary>
public sealed class OverlayPopUpGate
{
    AppData? _data;
    OverlaySettings? _settings;
    DateTimeOffset _checkedAt;
    DateTimeOffset? _start;

    /// <summary>False only when <see cref="UpcomingQuery.ForOverlay"/> is empty for <paramref name="data"/> and
    /// <paramref name="settings"/> at <paramref name="now"/>.</summary>
    public bool MayBeDue(AppData data, OverlaySettings settings, DateTimeOffset now)
    {
        if (!ReferenceEquals(data, _data) || !ReferenceEquals(settings, _settings) || now < _checkedAt || _start <= now)
        {
            _data = data;
            _settings = settings;
            _checkedAt = now;
            _start = UpcomingQuery.OverlayStart(data, settings, now);
        }
        return _start <= now;
    }
}
