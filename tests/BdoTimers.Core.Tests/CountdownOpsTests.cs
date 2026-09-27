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
    public void StartFrom_runs_as_if_started_then()
    {
        var c = CountdownOps.StartFrom(Hour, T0.AddMinutes(-40));
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Equal(T0.AddMinutes(-40), c.StartedAtUtc);
        Assert.Equal(T0.AddMinutes(20), c.EndsAtUtc);
    }

    [Fact]
    public void StartFrom_replaces_a_paused_run()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(10));
        var c = CountdownOps.StartFrom(paused, T0.AddMinutes(-5));
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Null(c.Remaining);
        Assert.Equal(T0.AddMinutes(55), c.EndsAtUtc);
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
    public void Complete_goes_idle()
    {
        var c = CountdownOps.Complete(CountdownOps.Start(Hour, T0));
        Assert.Equal(CountdownStatus.Idle, c.Status);
    }

    [Fact]
    public void Start_records_start_time()
    {
        Assert.Equal(T0, CountdownOps.Start(Hour, T0).StartedAtUtc);
    }

    [Fact]
    public void Pause_and_resume_keep_start_time()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(20));
        var resumed = CountdownOps.Resume(paused, T0.AddMinutes(30));
        Assert.Equal(T0, paused.StartedAtUtc);
        Assert.Equal(T0, resumed.StartedAtUtc);
    }

    [Fact]
    public void Reset_clears_start_time()
    {
        Assert.Null(CountdownOps.Reset(CountdownOps.Start(Hour, T0)).StartedAtUtc);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(43, 567.6)] // 43 % of 22 h = 9 h 27 m 36 s
    [InlineData(100, 1320)]
    public void StartForProgress_goes_back_that_share_of_the_duration(int percent, double minutesAgo)
    {
        Assert.Equal(T0.AddMinutes(-minutesAgo), CountdownOps.StartForProgress(TimeSpan.FromHours(22), percent, T0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(567.6, 43)]
    [InlineData(580, 43)] // 43.9 %: rounded down, as the game shows it
    [InlineData(1320, 100)]
    [InlineData(1400, 100)]
    [InlineData(-5, 0)]
    public void ProgressPercent_is_the_whole_share_elapsed(double elapsedMinutes, int percent)
    {
        Assert.Equal(percent, CountdownOps.ProgressPercent(TimeSpan.FromHours(22), TimeSpan.FromMinutes(elapsedMinutes)));
    }

    [Fact]
    public void Progress_round_trips_through_its_start()
    {
        var farm = TimeSpan.FromHours(22);
        for (var percent = 0; percent <= 100; percent++)
            Assert.Equal(percent, CountdownOps.ProgressPercent(farm, T0 - CountdownOps.StartForProgress(farm, percent, T0)));
    }
}
