namespace BdoTimers.Core.Tests;

/// <summary>Instants the tests share. 2026-09-22 is a Tuesday; Berlin is UTC+2 until 2026-10-25.</summary>
public static class TestTimes
{
    /// <summary>Tuesday 12:00 UTC, 14:00 in Berlin.</summary>
    public static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Tuesday 12:00 in Berlin.</summary>
    public static readonly DateTimeOffset BerlinNoon = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A day of September 2026, in UTC.</summary>
    public static DateTimeOffset Utc(int day, int hour, int minute) => new(2026, 9, day, hour, minute, 0, TimeSpan.Zero);
}
