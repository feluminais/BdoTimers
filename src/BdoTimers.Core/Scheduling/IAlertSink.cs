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
    /// <summary>
    /// The lines upcoming alerts will speak, soonest first: synthesize the ones not ready yet, so alerts don't load the
    /// voice as they play. Called every tick while one is due within a minute, so it must return at once and be cheap
    /// for lines already prepared.
    /// </summary>
    void PrepareSpeech(IReadOnlyCollection<string> texts);
}
