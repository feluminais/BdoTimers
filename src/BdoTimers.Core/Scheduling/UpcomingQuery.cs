using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public sealed record UpcomingItem(TimerDef Timer, DateTimeOffset AtUtc);

public static class UpcomingQuery
{
    /// <summary>How far ahead <see cref="OverlayStart"/> looks: a week and a day, so weekly times are always found.</summary>
    static readonly TimeSpan Reach = TimeSpan.FromDays(8);

    /// <summary>Unmuted occurrences of overlay-enabled timers inside each timer's "show N minutes before" window.</summary>
    public static IReadOnlyList<UpcomingItem> ForOverlay(AppData data, DateTimeOffset now)
    {
        var muted = data.Muted.ToHashSet();
        return data.Timers
            .Where(t => t.Enabled && t.Alerts.Overlay.Enabled)
            .SelectMany(t => OccurrenceSource.Between(t, now, now + TimeSpan.FromMinutes(t.Alerts.Overlay.ShowMinutesBefore))
                .Select(at => new UpcomingItem(t, at)))
            .Where(i => !muted.Contains(new MutedOccurrence(i.Timer.Id, i.AtUtc)))
            .OrderBy(i => i.AtUtc)
            .ToList();
    }

    /// <summary>
    /// The earliest instant from <paramref name="now"/> on at which <see cref="ForOverlay"/> can return anything while
    /// the data stays the same; at or before <paramref name="now"/> when a pop-up may be due now, null when no timer has
    /// overlay pop-ups. A timer with nothing in <see cref="Reach"/> counts as starting when the end of it enters its window.
    /// </summary>
    public static DateTimeOffset? OverlayStart(AppData data, DateTimeOffset now)
    {
        var muted = data.Muted.ToHashSet();
        DateTimeOffset? earliest = null;
        foreach (var t in data.Timers.Where(t => t.Enabled && t.Alerts.Overlay.Enabled))
        {
            var first = OccurrenceSource.Between(t, now, now + Reach)
                .Where(at => !muted.Contains(new MutedOccurrence(t.Id, at)))
                .Cast<DateTimeOffset?>()
                .FirstOrDefault();
            var start = (first ?? now + Reach) - TimeSpan.FromMinutes(t.Alerts.Overlay.ShowMinutesBefore);
            if (earliest is null || start < earliest) earliest = start;
        }
        return earliest;
    }
}

/// <summary>
/// Whether an overlay pop-up can be due, worked out once per data instance and kept until the earliest one can start,
/// so a tick with nothing else to show the overlay for can skip building it. Not thread-safe.
/// </summary>
public sealed class OverlayPopUpGate
{
    AppData? _data;
    DateTimeOffset _checkedAt;
    DateTimeOffset? _start;

    /// <summary>False only when <see cref="UpcomingQuery.ForOverlay"/> is empty for <paramref name="data"/> at
    /// <paramref name="now"/>.</summary>
    public bool MayBeDue(AppData data, DateTimeOffset now)
    {
        if (!ReferenceEquals(data, _data) || now < _checkedAt || _start <= now)
        {
            _data = data;
            _checkedAt = now;
            _start = UpcomingQuery.OverlayStart(data, now);
        }
        return _start <= now;
    }
}
