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

    /// <summary>A built-in boss spawning at the given times in Berlin.</summary>
    public static TimerDef Boss(string name, params (DayOfWeek Day, int Hour, int Minute)[] slots) => new()
    {
        Name = name,
        Kind = TimerKind.Scheduled,
        IsBuiltIn = true,
        Scheduled = new ScheduledSpec
        {
            TimeZoneId = "Europe/Berlin",
            Slots = slots.Select(s => new Slot(s.Day, new TimeOnly(s.Hour, s.Minute))).ToList(),
        },
    };

    public static TimerDef Boss(string name, DayOfWeek day, int hour, int minute = 0) => Boss(name, (day, hour, minute));
}
