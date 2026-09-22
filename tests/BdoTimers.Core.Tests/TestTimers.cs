using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public static class TestTimers
{
    public static TimerDef Countdown(DateTimeOffset endsAt, params int[] leads) => new()
    {
        Name = "Farm",
        Kind = TimerKind.Countdown,
        Countdown = new CountdownSpec
        {
            Duration = TimeSpan.FromMinutes(60),
            Status = CountdownStatus.Running,
            EndsAtUtc = endsAt,
        },
        Alerts = new AlertConfig { LeadTimesMinutes = leads },
    };

    public static TimerDef Scheduled(string name, DayOfWeek day, int hour, int minute, params int[] leads) => new()
    {
        Name = name,
        Kind = TimerKind.Scheduled,
        Scheduled = new ScheduledSpec
        {
            TimeZoneId = "Europe/Berlin",
            Slots = [new Slot(day, new TimeOnly(hour, minute))],
        },
        Alerts = new AlertConfig { LeadTimesMinutes = leads },
    };
}
