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

    /// <summary>The next <paramref name="count"/> occurrences at or after <paramref name="fromUtc"/>, ascending.</summary>
    public static IReadOnlyList<DateTimeOffset> Next(ScheduledSpec spec, DateTimeOffset fromUtc, int count)
    {
        var result = new List<DateTimeOffset>();
        if (count <= 0 || spec.Slots.Count == 0) return result;

        var tz = TimeZones.Find(spec.TimeZoneId);
        var day = TimeZoneInfo.ConvertTime(fromUtc, tz).Date.AddDays(-1);
        for (var i = 0; i < 400 && result.Count < count; i++, day = day.AddDays(1))
        {
            var date = day;
            result.AddRange(spec.Slots
                .Where(s => s.Day == date.DayOfWeek)
                .Select(s => LocalToUtc(date + s.Time.ToTimeSpan(), tz))
                .Where(o => o >= fromUtc)
                .Distinct()
                .Order());
        }
        return result.Take(count).ToList();
    }
}
