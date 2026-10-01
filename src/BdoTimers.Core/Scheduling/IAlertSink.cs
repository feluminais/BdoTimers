using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>One alert; <see cref="Timers"/> holds every timer sharing the spawn, sorted by name.</summary>
public sealed record AlertEvent(IReadOnlyList<TimerDef> Timers, DateTimeOffset OccurrenceUtc, int LeadMinutes, int MinutesLeft);

/// <summary>Receives alerts from the scheduler thread. Implementations must not throw.</summary>
public interface IAlertSink
{
    void Dispatch(AlertEvent alert);
    /// <summary>A countdown ended without its alert: the app was closed, the PC asleep or the scheduler stalled.</summary>
    void NotifyEndedWhileAway(TimerDef timer);
    /// <summary>A spoken alert is due soon: get the voice ready so it doesn't load while the alert plays. Called every
    /// tick until then, so it must be cheap once ready.</summary>
    void PrepareSpeech();
}
