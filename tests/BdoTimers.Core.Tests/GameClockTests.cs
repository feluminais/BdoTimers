using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class GameClockTests
{
    [Theory]
    [InlineData("2026-10-02T00:20:00Z", "07:00", false)]
    [InlineData("2026-10-02T20:20:00Z", "07:00", false)]
    [InlineData("2026-10-02T02:00:00Z", "14:30", false)]
    [InlineData("2026-10-02T03:39:00Z", "21:55:30", false)]
    [InlineData("2026-10-02T03:40:00Z", "22:00", true)]
    [InlineData("2026-10-02T00:00:00Z", "02:30", true)]
    [InlineData("2026-10-02T04:19:00Z", "06:46:30", true)]
    [InlineData("2026-10-02T04:20:00Z", "07:00", false)]
    // Night began at 14:40 EST on 13 December 2022 (forum report), the same instant as 19:40 UTC.
    [InlineData("2022-12-13T14:40:00-05:00", "22:00", true)]
    public void Maps_real_time_to_the_world_clock(string now, string time, bool night)
    {
        var at = GameClock.At(DateTimeOffset.Parse(now));

        Assert.Equal(TimeOnly.Parse(time), at.Time);
        Assert.Equal(night, at.IsNight);
    }

    [Fact]
    public void Repeats_every_four_hours_whatever_the_date()
    {
        var start = new DateTimeOffset(2026, 3, 29, 1, 7, 0, TimeSpan.Zero);
        for (var cycles = 1; cycles <= 12; cycles++)
            Assert.Equal(GameClock.At(start), GameClock.At(start.AddHours(4 * cycles)));
    }
}
