using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

/// <summary>Rechecks timer state, occurrence and current channel settings before queued playback.</summary>
public static class AlertEligibility
{
    public static AlertEvent? Filter(AppData data, AlertEvent alert)
    {
        var timers = alert.Timers.Select(t => ForPlayback(data, alert, t)).OfType<TimerDef>().ToList();
        return timers.Count == 0 ? null : timers.SequenceEqual(alert.Timers) ? alert : alert with { Timers = timers };
    }

    static TimerDef? ForPlayback(AppData data, AlertEvent alert, TimerDef planned)
    {
        var completion = alert.LeadMinutes == 0 ? data.CompletedCountdowns.FirstOrDefault(t => t.Id == planned.Id
            && t.Countdown == planned.Countdown) : null;
        var completed = completion is not null;
        var saved = data.Timers.FirstOrDefault(t => t.Id == planned.Id)
            ?? (planned.Preset == Presets.HorseRegistrationRun ? completion : null);
        if (saved is not { Enabled: true } || saved.Kind != planned.Kind
            || data.Muted.Contains(new MutedOccurrence(planned.Id, alert.OccurrenceUtc))
            || GarmothTracker.Gone(data, saved, alert.OccurrenceUtc)) return null;
        if (planned.IsBuiltIn && (alert.BossSelectionVersion != data.BossSelectionVersion
            || !BossRegions.IsSelected(data, saved))) return null;

        var sameOccurrence = planned.Kind switch
        {
            TimerKind.OneTime => SameEvent(saved, planned),
            TimerKind.Countdown => saved.Countdown is { Status: CountdownStatus.Running } countdown
                ? countdown == planned.Countdown : completed && saved.Countdown?.Status == CountdownStatus.Idle,
            TimerKind.Scheduled => OccurrenceSource.Next(saved, alert.OccurrenceUtc) == alert.OccurrenceUtc,
            _ => false,
        };
        if (!sameOccurrence) return null;
        return planned.Name == saved.Name && planned.Alerts == saved.Alerts ? planned
            : planned with { Name = saved.Name, Alerts = saved.Alerts };
    }

    static bool SameEvent(TimerDef current, TimerDef planned) => planned.OneTime is { } occurrence
        && current.OneTime is { } saved
        // Completion follows dispatch immediately; it must not cancel the sound queued by that dispatch.
        && saved with { Finished = false } == occurrence with { Finished = false };
}
