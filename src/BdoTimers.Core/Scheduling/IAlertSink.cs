using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public sealed record AlertEvent(TimerDef Timer, DateTimeOffset OccurrenceUtc, int LeadMinutes, int MinutesLeft);

/// <summary>Receives alerts from the scheduler thread. Implementations must not throw.</summary>
public interface IAlertSink
{
    void Dispatch(AlertEvent alert);
    void NotifyEndedWhileAway(TimerDef timer);
}
