using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class OccurrenceSource
{
    /// <summary>How far <see cref="Next"/> looks: a week and a day, so weekly times are always found.</summary>
    static readonly TimeSpan Reach = TimeSpan.FromDays(8);

    /// <summary>Occurrences of <paramref name="timer"/> within [fromUtc, toUtc], ascending; stopwatches have none.</summary>
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

    /// <summary>The first occurrence at or after <paramref name="now"/>; null when there is none.</summary>
    public static DateTimeOffset? Next(TimerDef timer, DateTimeOffset now) =>
        Between(timer, now, now + Reach).Cast<DateTimeOffset?>().FirstOrDefault();
}
