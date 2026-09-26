using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class StartTimesTests
{
    static readonly TimeZoneInfo Kyiv = TimeZones.Find("Europe/Kyiv");

    [Fact]
    public void A_time_earlier_today_is_today()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero); // 15:00 in Kyiv
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 11, 20, 0, TimeSpan.Zero), StartTimes.MostRecent(new TimeOnly(14, 20), now, Kyiv));
    }

    [Fact]
    public void A_time_still_ahead_today_is_yesterday()
    {
        var now = new DateTimeOffset(2026, 9, 25, 22, 0, 0, TimeSpan.Zero); // 01:00 on the 26th in Kyiv
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 20, 30, 0, TimeSpan.Zero), StartTimes.MostRecent(new TimeOnly(23, 30), now, Kyiv));
    }

    [Fact]
    public void The_current_minute_is_now()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(now, StartTimes.MostRecent(new TimeOnly(15, 0), now, Kyiv));
    }
}
