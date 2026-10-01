using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class AlertPauseTests
{
    [Fact]
    public void Timed_pause_expires_and_reports_remaining()
    {
        var s = AlertPause.Pause(new AppSettings(), T0, TimeSpan.FromHours(1));

        Assert.True(AlertPause.IsPaused(s, T0.AddMinutes(59)));
        Assert.Equal(TimeSpan.FromMinutes(20), AlertPause.Remaining(s, T0.AddMinutes(40)));
        Assert.False(AlertPause.IsPaused(s, T0.AddHours(1)));
        Assert.Null(AlertPause.Remaining(s, T0.AddHours(1)));
    }

    [Fact]
    public void Pause_until_resumed_has_no_remaining_time()
    {
        var s = AlertPause.Pause(new AppSettings(), T0, null);

        Assert.True(AlertPause.IsPaused(s, T0.AddDays(365)));
        Assert.Null(AlertPause.Remaining(s, T0));
        Assert.False(AlertPause.IsPaused(AlertPause.Resume(s), T0));
    }
}
