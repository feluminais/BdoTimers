using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>Decides which alerts are due on a tick. Holds the set of already-fired leads.</summary>
public sealed class AlertPlanner
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);
    /// <summary>
    /// How long after an occurrence its fired alerts are remembered: a clock set back by less than this replays
    /// nothing, while a larger jump counts as setting a new time and its alerts follow the new clock.
    /// </summary>
    public static readonly TimeSpan Memory = TimeSpan.FromHours(1);

    readonly HashSet<(Guid TimerId, DateTimeOffset Occurrence, int Lead)> _fired = [];
    readonly HashSet<Guid> _failed = [];

    /// <param name="defaultLeads">The default alert times from Settings, for timers without their own.</param>
    public IReadOnlyList<AlertEvent> Tick(
        IEnumerable<TimerDef> timers, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now,
        IReadOnlyList<int>? defaultLeads = null)
    {
        var events = new List<AlertEvent>();
        foreach (var timer in timers.Where(t => t.Enabled))
        {
            var leads = Leads(timer, defaultLeads);
            if (leads.Length == 0) continue;

            try
            {
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
            catch (Exception ex) { Failed(timer, ex); }
        }
        _fired.RemoveWhere(k => k.Occurrence < now - Memory);
        return events;
    }

    /// <summary>True when an alert that speaks is due after <paramref name="now"/> and within <paramref name="window"/>.</summary>
    public static bool SpeechDueWithin(
        IEnumerable<TimerDef> timers, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now, TimeSpan window,
        IReadOnlyList<int>? defaultLeads = null)
    {
        foreach (var timer in timers.Where(t => t.Enabled && t.Alerts.Tts.Enabled))
        {
            var leads = timer.Alerts.LeadTimes(defaultLeads ?? AlertConfig.StandardLeadTimesMinutes).Where(l => l >= 0).ToArray();
            if (leads.Length == 0) continue;
            IReadOnlyList<DateTimeOffset> occurrences;
            // A timer that can't be scheduled is logged by Tick.
            try { occurrences = OccurrenceSource.Between(timer, now, now + window + TimeSpan.FromMinutes(leads.Max())).ToList(); }
            catch (Exception) { continue; }
            foreach (var occurrence in occurrences)
            {
                if (muted.Contains(new MutedOccurrence(timer.Id, occurrence))) continue;
                if (leads.Select(l => occurrence - TimeSpan.FromMinutes(l)).Any(due => due > now && due <= now + window)) return true;
            }
        }
        return false;
    }

    static int[] Leads(TimerDef timer, IReadOnlyList<int>? defaultLeads) =>
        timer.Alerts.LeadTimes(defaultLeads ?? AlertConfig.StandardLeadTimesMinutes)
            .Where(l => l >= 0).Distinct().Order().ToArray();

    static int MinutesLeft(DateTimeOffset occurrence, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Ceiling((occurrence - now).TotalMinutes));

    /// <summary>A timer whose times can't be worked out (e.g. an unknown time zone) is skipped, so the others still
    /// alert; it is logged once rather than every tick.</summary>
    void Failed(TimerDef timer, Exception ex)
    {
        if (_failed.Add(timer.Id)) Log.Error($"Timer '{timer.Name}' can't be scheduled and won't alert", ex);
    }
}
