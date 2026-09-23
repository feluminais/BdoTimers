using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public sealed record UpcomingItem(TimerDef Timer, DateTimeOffset AtUtc, bool Muted);

public static class UpcomingQuery
{
    public static IReadOnlyList<UpcomingItem> Next(AppData data, DateTimeOffset now, TimeSpan horizon, int max)
    {
        var muted = data.Muted.ToHashSet();
        return data.Timers
            .Where(t => t.Enabled)
            .SelectMany(t => OccurrenceSource.Between(t, now, now + horizon)
                .Select(at => new UpcomingItem(t, at, muted.Contains(new MutedOccurrence(t.Id, at)))))
            .OrderBy(i => i.AtUtc)
            .ThenBy(i => i.Timer.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    public static IReadOnlyList<UpcomingItem> ForOverlay(AppData data, DateTimeOffset now)
    {
        var muted = data.Muted.ToHashSet();
        return data.Timers
            .Where(t => t.Enabled && t.Alerts.Overlay.Enabled)
            .SelectMany(t => OccurrenceSource.Between(t, now, now + TimeSpan.FromMinutes(t.Alerts.Overlay.ShowMinutesBefore))
                .Select(at => new UpcomingItem(t, at, false)))
            .Where(i => !muted.Contains(new MutedOccurrence(i.Timer.Id, i.AtUtc)))
            .OrderBy(i => i.AtUtc)
            .ToList();
    }
}
