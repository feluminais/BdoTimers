using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Text;

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

    readonly HashSet<(Guid TimerId, Guid ScheduleVersion, DateTimeOffset Occurrence, int Lead)> _fired = [];
    readonly HashSet<Guid> _failed = [];

    /// <param name="defaultLeads">The default alert times from Settings, for timers without their own.</param>
    public IReadOnlyList<AlertEvent> Tick(
        IEnumerable<TimerDef> timers, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now,
        IReadOnlyList<int>? defaultLeads = null, DateTimeOffset? bossAlertsAfterUtc = null)
    {
        var events = new List<AlertEvent>();
        foreach (var timer in timers.Where(t => t.Enabled))
        {
            var leads = Leads(timer, defaultLeads);
            if (leads.Length == 0) continue;

            try { events.AddRange(Due(timer, leads, muted, now, bossAlertsAfterUtc)); }
            catch (Exception ex) { Failed(timer, ex); }
        }
        _fired.RemoveWhere(k => k.Occurrence < now - Memory);
        return events;
    }

    IEnumerable<AlertEvent> Due(
        TimerDef timer, int[] leads, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now, DateTimeOffset? bossAlertsAfterUtc)
    {
        foreach (var occurrence in OccurrenceSource.Between(timer, now - Grace, now + TimeSpan.FromMinutes(leads[^1])))
        {
            if (muted.Contains(new MutedOccurrence(timer.Id, occurrence))) continue;
            if (NewlyDueLead(timer, occurrence, leads, now, bossAlertsAfterUtc) is { } fired)
                yield return new AlertEvent([timer], occurrence, fired, MinutesLeft(occurrence, now));
        }
    }

    /// <summary>Marks every due lead as fired. Ascending leads: the first newly-due one is the most recent and the
    /// one returned; older ones are marked silently.</summary>
    int? NewlyDueLead(TimerDef timer, DateTimeOffset occurrence, int[] leads, DateTimeOffset now, DateTimeOffset? bossAlertsAfterUtc)
    {
        int? toFire = null;
        foreach (var lead in leads)
        {
            if (now < occurrence - TimeSpan.FromMinutes(lead)) continue;
            var first = _fired.Add((timer.Id, EventVersion(timer), occurrence, lead));
            if (SuppressedAfterRegionSwitch(timer, occurrence - TimeSpan.FromMinutes(lead), bossAlertsAfterUtc)) continue;
            if (first && toFire is null) toFire = lead;
        }
        return toFire;
    }

    /// <summary>
    /// The speech of every spoken alert still to come for each occurrence whose next spoken alert is due within
    /// <paramref name="window"/>, soonest first, so one preparation covers an occurrence's whole sequence. Lines are worded
    /// as <see cref="Tick"/> and <see cref="AlertGrouping"/> word them when each alert comes due on time.
    /// </summary>
    public IReadOnlyList<string> UpcomingSpeech(
        IEnumerable<TimerDef> timers, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now, TimeSpan window,
        IReadOnlyList<int>? defaultLeads = null, DateTimeOffset? bossAlertsAfterUtc = null)
    {
        var planned = timers.Where(t => t.Enabled).Select(t => (Timer: t, Leads: Leads(t, defaultLeads)))
            .Where(p => p.Leads.Length > 0).ToList();
        if (!planned.Any(p => p.Timer.Alerts.Tts.Enabled)) return [];

        // Every timer is looked at as far ahead as the longest lead: a silent one still names itself in a shared line.
        var until = now + window + TimeSpan.FromMinutes(planned.Max(p => p.Leads[^1]));
        var pending = new List<AlertEvent>();
        foreach (var (timer, leads) in planned)
        {
            try { pending.AddRange(Pending(timer, leads, muted, now, until, bossAlertsAfterUtc)); }
            catch (Exception ex) { Failed(timer, ex); }
        }
        if (pending.Count == 0) return [];

        return AlertGrouping.Group(pending)
            .Where(a => a.Timers.Any(t => t.Alerts.Tts.Enabled))
            .GroupBy(a => a.OccurrenceUtc)
            .Where(occurrence => occurrence.Min(Due) <= now + window)
            .SelectMany(occurrence => occurrence)
            .OrderBy(Due)
            .Select(a => AlertMessage.Build(a).Speech)
            .Distinct()
            .ToList();

        static DateTimeOffset Due(AlertEvent a) => a.OccurrenceUtc - TimeSpan.FromMinutes(a.LeadMinutes);
    }

    /// <summary>
    /// A boss alert that came due at or before <paramref name="bossAlertsAfterUtc"/>, the moment a region was selected,
    /// stays silent, so switching regions doesn't replay leads that were already due. Custom timers are unaffected.
    /// </summary>
    internal static bool SuppressedAfterRegionSwitch(TimerDef timer, DateTimeOffset dueUtc, DateTimeOffset? bossAlertsAfterUtc) =>
        timer.IsBuiltIn && bossAlertsAfterUtc is { } boundary && dueUtc <= boundary;

    IEnumerable<AlertEvent> Pending(
        TimerDef timer, int[] leads, IReadOnlySet<MutedOccurrence> muted, DateTimeOffset now, DateTimeOffset until,
        DateTimeOffset? bossAlertsAfterUtc)
    {
        foreach (var occurrence in OccurrenceSource.Between(timer, now, until))
        {
            if (muted.Contains(new MutedOccurrence(timer.Id, occurrence))) continue;
            foreach (var lead in leads.Where(l => IsPending(timer, occurrence, l, now, bossAlertsAfterUtc)))
                yield return new AlertEvent([timer], occurrence, lead, lead);
        }
    }

    bool IsPending(TimerDef timer, DateTimeOffset occurrence, int lead, DateTimeOffset now, DateTimeOffset? bossAlertsAfterUtc)
    {
        var due = occurrence - TimeSpan.FromMinutes(lead);
        return due > now
            && !SuppressedAfterRegionSwitch(timer, due, bossAlertsAfterUtc)
            && !_fired.Contains((timer.Id, EventVersion(timer), occurrence, lead));
    }

    static int[] Leads(TimerDef timer, IReadOnlyList<int>? defaultLeads) =>
        timer.Alerts.LeadTimes(defaultLeads ?? AlertConfig.StandardLeadTimesMinutes)
            .Where(l => l >= 0).Distinct().Order().ToArray();

    static Guid EventVersion(TimerDef timer) => timer.Kind == TimerKind.OneTime ? timer.OneTime?.ScheduleVersion ?? Guid.Empty : Guid.Empty;

    static int MinutesLeft(DateTimeOffset occurrence, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Ceiling((occurrence - now).TotalMinutes));

    /// <summary>A timer whose times can't be worked out (e.g. an unknown time zone) is skipped, so the others still
    /// alert; it is logged once rather than every tick.</summary>
    void Failed(TimerDef timer, Exception ex)
    {
        if (_failed.Add(timer.Id)) Log.Error($"Timer '{timer.Name}' can't be scheduled and won't alert", ex);
    }
}
