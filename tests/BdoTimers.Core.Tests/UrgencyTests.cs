using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;

namespace BdoTimers.Core.Tests;

public class UrgencyTests
{
    [Theory]
    [InlineData(120, UrgencyLevel.Normal)]
    [InlineData(30.01, UrgencyLevel.Normal)]
    [InlineData(30, UrgencyLevel.Soon)]
    [InlineData(10.01, UrgencyLevel.Soon)]
    [InlineData(10, UrgencyLevel.Imminent)]
    [InlineData(1.01, UrgencyLevel.Imminent)]
    [InlineData(1, UrgencyLevel.Now)]
    [InlineData(0, UrgencyLevel.Now)]
    [InlineData(-5, UrgencyLevel.Now)]
    public void A_spawn_gets_more_urgent_at_thirty_ten_and_one_minute(double minutes, UrgencyLevel expected) =>
        Assert.Equal(expected, Urgency.Of(TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(0, "now")]
    [InlineData(-30, "now")]
    [InlineData(42 * 60, "in 42m")]
    [InlineData(10 * 60, "in 10m")]
    [InlineData(9 * 60 + 59, "in 9m 59s")]
    [InlineData(8 * 60 + 20, "in 8m 20s")]
    [InlineData(45, "in 0m 45s")]
    [InlineData(90 * 60, "in 1h 30m")]
    [InlineData(119 * 60 + 59, "in 1h 59m")]
    [InlineData(26 * 3600, "in 1d 02h")]
    public void Time_until_reads_as_hours_and_minutes_and_adds_seconds_under_ten_minutes(int seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.Until(TimeSpan.FromSeconds(seconds)));
}
