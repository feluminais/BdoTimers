using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class OneTimeEvents
{
    public static DateTimeOffset AtUtc(OneTimeSpec spec) =>
        ScheduleMath.LocalToUtc(spec.Date.ToDateTime(spec.Time), TimeZones.Find(spec.TimeZoneId));

    public static OneTimeSpec Create(IClock clock, string timeZoneId) => new()
    {
        Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZones.Find(timeZoneId)).DateTime).AddDays(1),
        Time = new(20, 0), TimeZoneId = timeZoneId,
    };
}
