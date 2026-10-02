namespace BdoTimers.Core.Scheduling;

/// <summary>The time of day in the game world, and whether it is night there.</summary>
public readonly record struct GameTime(TimeOnly Time, bool IsNight);

/// <summary>
/// Black Desert's world clock, shared by the NA and EU servers: a four-hour real cycle of 200 minutes of day (07:00 to
/// 22:00 in game) and 40 minutes of night (22:00 to 07:00). Day breaks at 00:20 UTC and every four hours after.
/// </summary>
public static class GameClock
{
    static readonly TimeSpan Cycle = TimeSpan.FromHours(4);
    static readonly TimeSpan Day = TimeSpan.FromMinutes(200);
    static readonly TimeSpan FirstDawnUtc = TimeSpan.FromMinutes(20);
    static readonly TimeOnly Dawn = new(7, 0);
    static readonly TimeOnly Dusk = new(22, 0);
    // In-game time passes 15 h per 200 real minutes by day and 9 h per 40 real minutes by night.
    const double DayRate = 15 * 60 / 200.0;
    const double NightRate = 9 * 60 / 40.0;

    public static GameTime At(DateTimeOffset now)
    {
        var sinceDawn = TimeSpan.FromTicks(Mod((now.UtcDateTime.TimeOfDay - FirstDawnUtc).Ticks, Cycle.Ticks));
        return sinceDawn < Day
            ? new(Dawn.Add(sinceDawn * DayRate), false)
            : new(Dusk.Add((sinceDawn - Day) * NightRate), true);
    }

    static long Mod(long value, long divisor) => (value % divisor + divisor) % divisor;
}
