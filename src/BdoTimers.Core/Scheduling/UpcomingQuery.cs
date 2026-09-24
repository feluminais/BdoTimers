using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public sealed record UpcomingItem(TimerDef Timer, DateTimeOffset AtUtc);

public static class UpcomingQuery
{
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
}
