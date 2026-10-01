using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class CountdownOpsTests
{
    static readonly CountdownSpec Hour = new() { Duration = TimeSpan.FromMinutes(60) };
    static readonly CountdownSpec Farm = new() { Duration = TimeSpan.FromHours(22) };

    [Theory]
    [InlineData(0)]
    [InlineData(-40)] // started late
    public void Start_runs_from_the_given_start(int startMinutes)
    {
        var start = T0.AddMinutes(startMinutes);
        var c = CountdownOps.Start(Hour, start);
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Equal(start, c.StartedAtUtc);
        Assert.Equal(start.AddMinutes(60), c.EndsAtUtc);
        Assert.Null(c.Remaining);
    }

    [Fact]
    public void Start_replaces_a_paused_run()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(10));
        var c = CountdownOps.Start(paused, T0.AddMinutes(-5));
        Assert.Equal(CountdownStatus.Running, c.Status);
        Assert.Null(c.Remaining);
        Assert.Equal(T0.AddMinutes(55), c.EndsAtUtc);
    }

    [Fact]
    public void Pause_then_resume_keeps_remaining_and_start_time()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(20));
        Assert.Equal(CountdownStatus.Paused, paused.Status);
        Assert.Equal(TimeSpan.FromMinutes(40), paused.Remaining);
        Assert.Null(paused.EndsAtUtc);
        Assert.Equal(T0, paused.StartedAtUtc);

        var resumed = CountdownOps.Resume(paused, T0.AddMinutes(30));
        Assert.Equal(CountdownStatus.Running, resumed.Status);
        Assert.Equal(T0.AddMinutes(70), resumed.EndsAtUtc);
        Assert.Equal(T0, resumed.StartedAtUtc);
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
        Assert.Null(c.StartedAtUtc);
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
    public void A_zero_duration_has_no_progress() =>
        Assert.Equal(0, CountdownOps.ProgressPercent(TimeSpan.Zero, TimeSpan.FromMinutes(5)));

    [Fact]
    public void Progress_round_trips_through_its_start()
    {
        var farm = TimeSpan.FromHours(22);
        for (var percent = 0; percent <= 100; percent++)
            Assert.Equal(percent, CountdownOps.ProgressPercent(farm, T0 - CountdownOps.StartForProgress(farm, percent, T0)));
    }

    [Fact]
    public void Progress_of_a_running_countdown_is_the_share_elapsed()
    {
        Assert.Equal(33, CountdownOps.Progress(CountdownOps.Start(Hour, T0), T0.AddMinutes(20)));
        Assert.Equal(100, CountdownOps.Progress(CountdownOps.Start(Hour, T0), T0.AddMinutes(75)));
    }

    [Fact]
    public void Progress_does_not_count_time_spent_paused()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Hour, T0), T0.AddMinutes(20));
        Assert.Equal(33, CountdownOps.Progress(paused, T0.AddMinutes(50)));
        Assert.Equal(50, CountdownOps.Progress(CountdownOps.Resume(paused, T0.AddMinutes(50)), T0.AddMinutes(60)));
    }

    [Fact]
    public void Progress_of_an_idle_countdown_is_none()
    {
        Assert.Null(CountdownOps.Progress(Hour, T0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(60, 100)]
    [InlineData(90, 150)]
    [InlineData(120, 200)]
    [InlineData(150, 200)]
    public void Farm_growth_continues_past_zero_and_caps_at_200(int elapsedMinutes, int percent)
    {
        var running = CountdownOps.Start(Hour, T0);
        Assert.Equal(percent, CountdownOps.Progress(running, T0.AddMinutes(elapsedMinutes), overgrows: true));
    }

    [Fact]
    public void Pausing_overgrown_farm_freezes_negative_time_until_resumed()
    {
        var running = CountdownOps.Start(Hour, T0);
        var paused = CountdownOps.Pause(running, T0.AddMinutes(90), preserveOvergrowth: true);
        Assert.Equal(TimeSpan.FromMinutes(-30), paused.Remaining);
        Assert.Equal(150, CountdownOps.Progress(paused, T0.AddHours(5), overgrows: true));

        var resumed = CountdownOps.Resume(paused, T0.AddHours(5));
        Assert.Equal(T0.AddHours(4.5), resumed.EndsAtUtc);
        Assert.Equal(150, CountdownOps.Progress(resumed, T0.AddHours(5), overgrows: true));
    }

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

    [Fact]
    public void Changing_duration_of_paused_overgrown_farm_preserves_elapsed_growth()
    {
        var paused = CountdownOps.Pause(CountdownOps.Start(Farm, T0), T0.AddHours(33), preserveOvergrowth: true);

        var changed = CountdownOps.ChangeDuration(paused, TimeSpan.FromHours(20), preserveOvergrowth: true);

        Assert.Equal(TimeSpan.FromHours(-13), changed.Remaining);
        Assert.Equal(165, CountdownOps.Progress(changed, T0.AddDays(2), overgrows: true));
    }
}
