using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class TodoReset
{
    public static DateTimeOffset Next(TodoSchedule schedule, DateTimeOffset nowUtc)
    {
        if (schedule.Hour is < 0 or > 23 || schedule.Minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(schedule), "Reset time must be a valid 24-hour time.");
        var zone = schedule.LocalTime ? TimeZoneInfo.Local : TimeZoneInfo.Utc;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var day = localNow.Date;
        if (schedule.Cadence == TodoCadence.Weekly)
            day = day.AddDays(((int)schedule.Day - (int)day.DayOfWeek + 7) % 7);
        var candidate = day.AddHours(schedule.Hour).AddMinutes(schedule.Minute);
        var interval = schedule.Cadence == TodoCadence.Daily ? 1 : 7;
        var resolved = Resolve(candidate, zone);
        while (resolved <= nowUtc)
        {
            candidate = candidate.AddDays(interval);
            resolved = Resolve(candidate, zone);
        }
        return resolved;
    }

    static DateTimeOffset Resolve(DateTime local, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
