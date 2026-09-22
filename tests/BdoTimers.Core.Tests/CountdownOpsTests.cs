using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class CountdownOpsTests
{
    static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    static readonly CountdownSpec Hour = new() { Duration = TimeSpan.FromMinutes(60) };

    [Fact]
    public void Start_sets_end_time()
    {
        var c = CountdownOps.Start(Hour, T0);
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Equal(T0.AddMinutes(60), c.EndsAtUtc);
        Assert.Null(c.Remaining);
    }

    [Fact]
    public void Pause_then_resume_keeps_remaining_time()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(20));
        Assert.Equal(CountdownStatus.Paused, paused.Status);
        Assert.Equal(TimeSpan.FromMinutes(40), paused.Remaining);
        Assert.Null(paused.EndsAtUtc);

        var resumed = CountdownOps.Resume(paused, T0.AddMinutes(30));
        Assert.Equal(CountdownStatus.Running, resumed.Status);
        Assert.Equal(T0.AddMinutes(70), resumed.EndsAtUtc);
    }

    [Fact]
    public void Pause_and_resume_ignore_wrong_states()
    {
        Assert.Equal(Hour, CountdownOps.Pause(Hour, T0));
        var running = CountdownOps.Start(Hour, T0);
        Assert.Equal(running, CountdownOps.Resume(running, T0));
    }

    [Fact]
    public void Reset_returns_to_idle()
    {
        var c = CountdownOps.Reset(CountdownOps.Start(Hour, T0));
        Assert.Equal(CountdownStatus.Idle, c.Status);
        Assert.Null(c.EndsAtUtc);
        Assert.Null(c.Remaining);
    }

    [Fact]
    public void Complete_without_repeat_goes_idle()
    {
        var c = CountdownOps.Complete(CountdownOps.Start(Hour, T0), T0.AddMinutes(60));
        Assert.Equal(CountdownStatus.Idle, c.Status);
    }

    [Fact]
    public void Complete_with_repeat_restarts_from_now()
    {
        var repeating = Hour with { AutoRepeat = true };
        var c = CountdownOps.Complete(CountdownOps.Start(repeating, T0), T0.AddMinutes(61));
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Equal(T0.AddMinutes(121), c.EndsAtUtc);
    }
}
