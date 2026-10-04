using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class ScheduleMath
{
    public const int MaxEveryWeeks = 52;

    public static void ValidateDateRange(DateOnly? start, DateOnly? end)
    {
        if (start is { } first && end is { } last && last < first)
            throw new ArgumentException("End date must be on or after start date.");
    }

    public static void ValidateEveryWeeks(int everyWeeks)
    {
        if (everyWeeks is < 1 or > MaxEveryWeeks)
            throw new ArgumentException($"Repeat every 1 to {MaxEveryWeeks} weeks.");
    }

    /// <summary>The Monday of <paramref name="date"/>'s week.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Whether the schedule runs in <paramref name="date"/>'s week.</summary>
    public static bool RunsInWeekOf(ScheduledSpec spec, DateOnly date)
    {
        if (spec.EveryWeeks <= 1) return true;
        var anchor = WeekStart(spec.WeekAnchor ?? spec.StartDate ?? new DateOnly(2001, 1, 1));
        var weeks = (WeekStart(date).DayNumber - anchor.DayNumber) / 7;
        return (weeks % spec.EveryWeeks + spec.EveryWeeks) % spec.EveryWeeks == 0;
    }

    /// <summary>The slot that <paramref name="occurrenceUtc"/> is an occurrence of, if any.</summary>
    public static Slot? SlotAt(ScheduledSpec spec, DateTimeOffset occurrenceUtc)
    {
        var local = TimeZoneInfo.ConvertTime(occurrenceUtc, TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneId));
        var time = TimeOnly.FromDateTime(local.DateTime);
        return spec.Slots.Where(s => s.Day == local.DayOfWeek && s.Time == time).Cast<Slot?>().FirstOrDefault();
    }

    public static bool IsExpired(ScheduledSpec spec, DateTimeOffset now) =>
        spec.EndDate is not null && !From(spec, now).Any();

    /// <summary>
    /// Converts a wall-clock time in <paramref name="tz"/> to UTC. A time inside a
    /// spring-forward gap is read with the pre-transition offset (so it lands after the
    /// gap); an ambiguous autumn time takes its first instance (the larger offset).
    /// </summary>
    public static DateTimeOffset LocalToUtc(DateTime local, TimeZoneInfo tz)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        TimeSpan offset;
        if (tz.IsInvalidTime(local))
            offset = tz.GetUtcOffset(local.AddHours(-3));
        else if (tz.IsAmbiguousTime(local))
            offset = tz.GetAmbiguousTimeOffsets(local).Max();
        else
            offset = tz.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    /// <summary>
    /// The latest moment, at or before <paramref name="nowUtc"/>, when the clock in <paramref name="zone"/> showed
    /// <paramref name="time"/>: today, or yesterday when today's is still ahead (23:30 picked at 01:00). It turns a time
    /// picked for "I started at…" into the moment it means.
    /// </summary>
    public static DateTimeOffset MostRecent(TimeOnly time, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        var today = TimeZoneInfo.ConvertTime(nowUtc, zone).Date + time.ToTimeSpan();
        var at = LocalToUtc(today, zone);
        return at <= nowUtc ? at : LocalToUtc(today.AddDays(-1), zone);
    }

    /// <summary>
    /// All occurrences at or after <paramref name="fromUtc"/>, ascending, within the optional inclusive date limits.
    /// Computed lazily so a short look-ahead only evaluates the days it needs.
    /// </summary>
    public static IEnumerable<DateTimeOffset> From(ScheduledSpec spec, DateTimeOffset fromUtc)
    {
        // Slots come from an editable file; a day outside DayOfWeek would never match and loop forever.
        if (!spec.Slots.Any(s => Enum.IsDefined(s.Day))) yield break;
        ValidateDateRange(spec.StartDate, spec.EndDate);
        ValidateEveryWeeks(spec.EveryWeeks);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneId);
        // Start a day early: a late slot on the previous local day that falls in a spring-forward gap
        // is pushed past midnight, into the day that contains fromUtc.
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fromUtc, tz).DateTime);
        if (first > DateOnly.MinValue) first = first.AddDays(-1);
        if (spec.StartDate is { } start && start > first) first = start;
        var last = spec.EndDate ?? DateOnly.MaxValue;
        for (var day = first; day <= last;)
        {
            var date = day;
            if (!RunsInWeekOf(spec, date))
            {
                // Straight to the next Monday: nothing in this week counts.
                var monday = WeekStart(date).AddDays(7);
                if (monday > last || monday < day) yield break;
                day = monday;
                continue;
            }
            var occurrences = spec.Slots
                .Where(s => s.Day == date.DayOfWeek)
                .Select(s => LocalToUtc(date.ToDateTime(s.Time), tz))
                .Where(o => o >= fromUtc)
                .Distinct()
                .Order();
            foreach (var occurrence in occurrences) yield return occurrence;
            if (day == last) yield break;
            day = day.AddDays(1);
        }
    }
}
