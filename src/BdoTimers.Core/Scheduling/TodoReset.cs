using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class TodoReset
{
    public static DateTimeOffset Next(TodoSchedule schedule, DateTimeOffset nowUtc, TimeZoneInfo? localZone = null)
    {
        if (schedule.Hour is < 0 or > 23 || schedule.Minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(schedule), "Reset time must be a valid 24-hour time.");
        var zone = schedule.LocalTime ? localZone ?? TimeZoneInfo.Local : TimeZoneInfo.Utc;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var day = localNow.Date;
        if (schedule.Cadence == TodoCadence.Weekly)
        {
            var resetDay = Enum.IsDefined(schedule.Day) ? schedule.Day : DayOfWeek.Thursday;
            day = day.AddDays(((int)resetDay - (int)day.DayOfWeek + 7) % 7);
        }
        var candidate = day.AddHours(schedule.Hour).AddMinutes(schedule.Minute);
        var interval = schedule.Cadence == TodoCadence.Daily ? 1 : 7;
        var resolved = ScheduleMath.LocalToUtc(candidate, zone);
        while (resolved <= nowUtc)
        {
            candidate = candidate.AddDays(interval);
            resolved = ScheduleMath.LocalToUtc(candidate, zone);
        }
        return resolved;
    }

}
