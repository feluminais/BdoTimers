using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>One alert; <see cref="Timers"/> holds every timer sharing the spawn, sorted by name.</summary>
public sealed record AlertEvent(IReadOnlyList<TimerDef> Timers, DateTimeOffset OccurrenceUtc, int LeadMinutes, int MinutesLeft);

/// <summary>Receives alerts from the scheduler thread. Implementations must not throw.</summary>
public interface IAlertSink
{
    void Dispatch(AlertEvent alert);
    void NotifyEndedWhileAway(TimerDef timer);
}
