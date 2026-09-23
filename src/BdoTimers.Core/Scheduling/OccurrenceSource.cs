using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class OccurrenceSource
{
    /// <summary>Occurrences of <paramref name="timer"/> within [fromUtc, toUtc], ascending.</summary>
    public static IEnumerable<DateTimeOffset> Between(TimerDef timer, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (timer.Kind == TimerKind.Scheduled && timer.Scheduled is { } spec)
            return ScheduleMath.From(spec, fromUtc).TakeWhile(o => o <= toUtc);

        if (timer.Kind == TimerKind.Countdown
            && timer.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end }
            && end >= fromUtc && end <= toUtc)
            return [end];

        return [];
    }
}
