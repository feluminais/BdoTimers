using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class ScheduleMath
{
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
    /// All occurrences at or after <paramref name="fromUtc"/>, ascending and unbounded; callers limit it
    /// with Take/TakeWhile. Computed lazily so a short look-ahead only evaluates the days it needs.
    /// </summary>
    public static IEnumerable<DateTimeOffset> From(ScheduledSpec spec, DateTimeOffset fromUtc)
    {
        // Slots come from an editable file; a day outside DayOfWeek would never match and loop forever.
        if (!spec.Slots.Any(s => Enum.IsDefined(s.Day))) yield break;

        var tz = TimeZones.Find(spec.TimeZoneId);
        // Start a day early: a late slot on the previous local day that falls in a spring-forward gap
        // is pushed past midnight, into the day that contains fromUtc.
        for (var day = TimeZoneInfo.ConvertTime(fromUtc, tz).Date.AddDays(-1); ; day = day.AddDays(1))
        {
            var date = day;
            var occurrences = spec.Slots
                .Where(s => s.Day == date.DayOfWeek)
                .Select(s => LocalToUtc(date + s.Time.ToTimeSpan(), tz))
                .Where(o => o >= fromUtc)
                .Distinct()
                .Order();
            foreach (var occurrence in occurrences) yield return occurrence;
        }
    }
}
