using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class AlertConfigTests
{
    [Fact]
    public void Timers_without_their_own_times_use_the_default()
    {
        var own = new AlertConfig { LeadTimesMinutes = [30] };
        var follows = new AlertConfig();

        Assert.Equal([30], own.LeadTimes([1, 0]));
        Assert.Equal([1, 0], follows.LeadTimes([1, 0]));
        Assert.True(own.OverridesDefaults);
        Assert.False(follows.OverridesDefaults);
        Assert.True((follows with { Sound = new SoundAlert { Key = "harp" } }).OverridesDefaults);
        Assert.True((follows with { Sound = new SoundAlert { Enabled = false } }).OverridesDefaults);
    }
}
