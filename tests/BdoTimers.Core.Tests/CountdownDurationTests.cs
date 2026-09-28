using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class CountdownDurationTests
{
    static readonly DateTimeOffset T0 = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
    static readonly CountdownSpec Farm = new() { Duration = TimeSpan.FromHours(22) };

    [Theory]
    [InlineData(20, 40)]
    [InlineData(21, 38)]
    [InlineData(22, 36)]
    public void Running_farm_keeps_elapsed_time_when_duration_changes(int hours, int progress)
    {
        var changed = CountdownOps.ChangeDuration(CountdownOps.Start(Farm, T0), TimeSpan.FromHours(hours));
        Assert.Equal(T0.AddHours(hours), changed.EndsAtUtc);
        Assert.Equal(T0, changed.StartedAtUtc);
        Assert.Equal(progress, CountdownOps.Progress(changed, T0.AddHours(8)));
        Assert.Equal(CountdownStatus.Running, changed.Status);
    }

    [Fact]
    public void Duration_change_after_pause_and_resume_excludes_paused_time()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Farm, T0), T0.AddHours(8));
        var changed = CountdownOps.ChangeDuration(paused, TimeSpan.FromHours(20));
        Assert.Equal(TimeSpan.FromHours(12), changed.Remaining);
        Assert.Equal(40, CountdownOps.Progress(changed, T0.AddHours(10)));
        var resumed = CountdownOps.Resume(changed, T0.AddHours(10));
        var longer = CountdownOps.ChangeDuration(resumed, TimeSpan.FromHours(21));
        Assert.Equal(T0.AddHours(23), longer.EndsAtUtc);
        Assert.Equal(38, CountdownOps.Progress(longer, T0.AddHours(10)));
    }

    [Fact]
    public void Shortening_below_elapsed_time_is_due_or_zero_remaining()
    {
        var running = CountdownOps.Start(Farm, T0);
        var shortened = CountdownOps.ChangeDuration(running, TimeSpan.FromHours(20));
        Assert.True(shortened.EndsAtUtc <= T0.AddHours(21));
        var paused = CountdownOps.Pause(running, T0.AddHours(21));
        Assert.Equal(TimeSpan.Zero, CountdownOps.ChangeDuration(paused, TimeSpan.FromHours(20)).Remaining);
    }

    [Fact]
    public void Idle_duration_changes_without_starting()
    {
        var changed = CountdownOps.ChangeDuration(Farm, TimeSpan.FromHours(20.5));
        Assert.Equal(TimeSpan.FromHours(20.5), changed.Duration);
        Assert.Equal(CountdownStatus.Idle, changed.Status);
        Assert.Null(changed.EndsAtUtc);
        Assert.Null(changed.Remaining);
    }
}
