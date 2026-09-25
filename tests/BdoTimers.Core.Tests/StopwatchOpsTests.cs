using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class StopwatchOpsTests
{
    static readonly DateTimeOffset T0 = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    static readonly StopwatchSpec Idle = new();

    [Fact]
    public void Counts_up_from_the_start()
    {
        var s = StopwatchOps.Start(Idle, T0);
        Assert.Equal(CountdownStatus.Running, s.Status);
        Assert.Equal(TimeSpan.FromMinutes(25), StopwatchOps.Elapsed(s, T0.AddMinutes(25)));
        Assert.Equal(TimeSpan.Zero, StopwatchOps.Elapsed(Idle, T0));
    }

    [Fact]
    public void Pause_holds_the_count_and_resume_continues_it()
    {
        var paused = StopwatchOps.Pause(StopwatchOps.Start(Idle, T0), T0.AddMinutes(20));
        Assert.Equal(TimeSpan.FromMinutes(20), StopwatchOps.Elapsed(paused, T0.AddHours(3)));

        var resumed = StopwatchOps.Resume(paused, T0.AddHours(3));
        Assert.Equal(TimeSpan.FromMinutes(30), StopwatchOps.Elapsed(resumed, T0.AddHours(3).AddMinutes(10)));
    }

    [Fact]
    public void Setting_the_elapsed_time_starts_an_idle_stopwatch_from_there()
    {
        var s = StopwatchOps.SetElapsed(Idle, T0, TimeSpan.FromMinutes(40));
        Assert.Equal(CountdownStatus.Running, s.Status);
        Assert.Equal(T0.AddMinutes(-40), s.StartedAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(45), StopwatchOps.Elapsed(s, T0.AddMinutes(5)));
    }

    [Fact]
    public void Setting_the_elapsed_time_keeps_a_paused_stopwatch_paused()
    {
        var paused = StopwatchOps.Pause(StopwatchOps.Start(Idle, T0), T0.AddMinutes(5));
        var s = StopwatchOps.SetElapsed(paused, T0.AddMinutes(6), TimeSpan.FromHours(2));
        Assert.Equal(CountdownStatus.Paused, s.Status);
        Assert.Equal(TimeSpan.FromHours(2), StopwatchOps.Elapsed(s, T0.AddHours(9)));
    }

    [Fact]
    public void Reset_goes_back_to_idle() =>
        Assert.Equal(Idle, StopwatchOps.Reset(StopwatchOps.Start(Idle, T0)));
}
