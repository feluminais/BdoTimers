using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class ScheduleMath
{
    public static void ValidateDateRange(DateOnly? start, DateOnly? end)
    {
        if (start is { } first && end is { } last && last < first)
            throw new ArgumentException("End date must be on or after start date.");
    }

    public static bool IsExpired(ScheduledSpec spec, DateTimeOffset now) =>
        spec.EndDate is not null && Next(spec, now, 1).Count == 0;

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

    /// <summary>The next <paramref name="count"/> occurrences at or after <paramref name="fromUtc"/>, ascending.</summary>
    public static IReadOnlyList<DateTimeOffset> Next(ScheduledSpec spec, DateTimeOffset fromUtc, int count) =>
        From(spec, fromUtc).Take(Math.Max(0, count)).ToList();

    /// <summary>
    /// All occurrences at or after <paramref name="fromUtc"/>, ascending, within the optional inclusive date limits.
    /// Computed lazily so a short look-ahead only evaluates the days it needs.
    /// </summary>
    public static IEnumerable<DateTimeOffset> From(ScheduledSpec spec, DateTimeOffset fromUtc)
    {
        // Slots come from an editable file; a day outside DayOfWeek would never match and loop forever.
        if (!spec.Slots.Any(s => Enum.IsDefined(s.Day))) yield break;
        ValidateDateRange(spec.StartDate, spec.EndDate);

        var tz = TimeZones.Find(spec.TimeZoneId);
        // Start a day early: a late slot on the previous local day that falls in a spring-forward gap
        // is pushed past midnight, into the day that contains fromUtc.
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fromUtc, tz).DateTime);
        if (first > DateOnly.MinValue) first = first.AddDays(-1);
        if (spec.StartDate is { } start && start > first) first = start;
        var last = spec.EndDate ?? DateOnly.MaxValue;
        for (var day = first; day <= last;)
        {
            var date = day;
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
