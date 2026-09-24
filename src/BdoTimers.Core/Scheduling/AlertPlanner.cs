using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>Decides which alerts are due on a tick. Holds the set of already-fired leads.</summary>
public sealed class AlertPlanner
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);

    readonly HashSet<(Guid TimerId, DateTimeOffset Occurrence, int Lead)> _fired = [];

    public IReadOnlyList<AlertEvent> Tick(
        IEnumerable<TimerDef> timers, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now)
    {
        var events = new List<AlertEvent>();
        foreach (var timer in timers.Where(t => t.Enabled))
        {
            var leads = timer.Alerts.LeadTimesMinutes.Where(l => l >= 0).Distinct().Order().ToArray();
            if (leads.Length == 0) continue;

            var window = OccurrenceSource.Between(timer, now - Grace, now + TimeSpan.FromMinutes(leads[^1]));
            foreach (var occurrence in window)
            {
                if (muted.Contains(new MutedOccurrence(timer.Id, occurrence))) continue;

                // Ascending leads: the first newly-due one is the most recent; older ones are marked silently.
                int? toFire = null;
                foreach (var lead in leads)
                {
                    if (now < occurrence - TimeSpan.FromMinutes(lead)) continue;
                    if (_fired.Add((timer.Id, occurrence, lead)) && toFire is null) toFire = lead;
                }
                if (toFire is { } fired)
                    events.Add(new AlertEvent([timer], occurrence, fired, MinutesLeft(occurrence, now)));
            }
        }
        _fired.RemoveWhere(k => k.Occurrence < now - Grace - Grace);
        return events;
    }

    static int MinutesLeft(DateTimeOffset occurrence, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Ceiling((occurrence - now).TotalMinutes));
}
