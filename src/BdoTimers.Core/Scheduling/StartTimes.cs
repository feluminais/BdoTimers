namespace BdoTimers.Core.Scheduling;

/// <summary>Turns a clock time the user picked for "I started at…" into the moment it means.</summary>
public static class StartTimes
{
    /// <summary>
    /// The latest moment, at or before <paramref name="nowUtc"/>, when the clock in <paramref name="zone"/> showed
    /// <paramref name="time"/>: today, or yesterday when today's is still ahead (23:30 picked at 01:00).
    /// </summary>
    public static DateTimeOffset MostRecent(TimeOnly time, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        var today = TimeZoneInfo.ConvertTime(nowUtc, zone).Date + time.ToTimeSpan();
        var at = ScheduleMath.LocalToUtc(today, zone);
        return at <= nowUtc ? at : ScheduleMath.LocalToUtc(today.AddDays(-1), zone);
    }
}
