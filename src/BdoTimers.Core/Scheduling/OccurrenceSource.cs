using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class OccurrenceSource
{
    public static DateTimeOffset? Next(TimerDef timer, DateTimeOffset fromUtc) =>
        From(timer, fromUtc).Cast<DateTimeOffset?>().FirstOrDefault();

    /// <summary>Occurrences of <paramref name="timer"/> within [fromUtc, toUtc], ascending; stopwatches have none.</summary>
    public static IEnumerable<DateTimeOffset> Between(TimerDef timer, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        From(timer, fromUtc).TakeWhile(o => o <= toUtc);

    public static IEnumerable<DateTimeOffset> From(TimerDef timer, DateTimeOffset fromUtc)
    {
        if (timer.Kind == TimerKind.Scheduled && timer.Scheduled is { } spec)
            return ScheduleMath.From(spec, fromUtc);

        if (timer.Kind == TimerKind.OneTime && timer.OneTime is { Finished: false } oneTime)
        {
            var at = OneTimeEvents.AtUtc(oneTime);
            return at >= fromUtc ? [at] : [];
        }

        if (timer.Kind == TimerKind.Countdown
            && timer.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end }
            && end >= fromUtc)
            return [end];

        return [];
    }
}
