using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class OverlayContentTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero); // Tue 12:00 Berlin
    static readonly OverlaySettings All = new();

    static TimerDef Boss(string name, int hour, int minute = 0) =>
        TestTimers.Scheduled(name, DayOfWeek.Tuesday, hour, minute, 0) with { IsBuiltIn = true };

    static TimerDef Farm(CountdownSpec spec) => Presets.Create().Single(p => p.Preset == Presets.Farm) with { Countdown = spec };

    static TimerDef Fishing(StopwatchSpec spec) =>
        Presets.Create().Single(p => p.Preset == Presets.Fishing) with { Stopwatch = spec };

    static TimerDef WithPopUp(TimerDef t, int minutes) =>
        t with { Alerts = t.Alerts with { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = minutes } } };

    [Fact]
    public void Shows_the_previous_and_next_spawn()
    {
        var data = new AppData { Timers = [Boss("Kzarka", 11), Boss("Nouver", 16)] };

        var s = OverlayContent.Build(data, All, Now);

        Assert.Equal("Kzarka", s.Previous!.Bosses.Single().Name);
        Assert.Equal("Nouver", s.Next!.Bosses.Single().Name);
        Assert.True(s.Clock);
        Assert.False(s.IsEmpty);
    }

    [Fact]
    public void Switched_off_sections_come_back_empty()
    {
        var data = new AppData
        {
            Timers =
            [
                Boss("Kzarka", 11),
                Farm(CountdownOps.Start(new CountdownSpec(), Now)),
                Fishing(StopwatchOps.Start(new StopwatchSpec(), Now)),
            ],
        };
        var none = new OverlaySettings { ShowClock = false, ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false };

        var s = OverlayContent.Build(data, none, Now);

        Assert.Null(s.Previous);
        Assert.Null(s.Next);
        Assert.Null(s.FarmLeft);
        Assert.Null(s.FarmProgress);
        Assert.Null(s.FishingElapsed);
        Assert.True(s.IsEmpty);
    }

    [Fact]
    public void Farm_shows_time_left_while_running_or_paused_and_nothing_when_idle()
    {
        var running = CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromHours(22) }, Now.AddHours(-2));
        Assert.Equal(TimeSpan.FromHours(20), OverlayContent.Build(new AppData { Timers = [Farm(running)] }, All, Now).FarmLeft);

        var paused = CountdownOps.Pause(running, Now);
        Assert.Equal(TimeSpan.FromHours(20),
            OverlayContent.Build(new AppData { Timers = [Farm(paused)] }, All, Now.AddHours(5)).FarmLeft);

        Assert.Null(OverlayContent.Build(new AppData { Timers = [Farm(new CountdownSpec())] }, All, Now).FarmLeft);
    }

    [Fact]
    public void Fishing_shows_elapsed_while_running_or_paused_and_nothing_when_idle()
    {
        var running = StopwatchOps.Start(new StopwatchSpec(), Now.AddMinutes(-42));
        Assert.Equal(TimeSpan.FromMinutes(42),
            OverlayContent.Build(new AppData { Timers = [Fishing(running)] }, All, Now).FishingElapsed);

        var paused = StopwatchOps.Pause(running, Now);
        Assert.Equal(TimeSpan.FromMinutes(42),
            OverlayContent.Build(new AppData { Timers = [Fishing(paused)] }, All, Now.AddHours(1)).FishingElapsed);

        Assert.Null(OverlayContent.Build(new AppData { Timers = [Fishing(new StopwatchSpec())] }, All, Now).FishingElapsed);
    }

    [Fact]
    public void Farm_overlay_keeps_signed_time_and_growth_after_harvest()
    {
        var running = CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromHours(22) }, Now.AddHours(-33));
        var current = OverlayContent.Build(new AppData { Timers = [Farm(running)] }, All, Now);
        Assert.Equal(TimeSpan.FromHours(-11), current.FarmLeft);
        Assert.Equal(150, current.FarmProgress);

        var paused = CountdownOps.Pause(running, Now, preserveOvergrowth: true);
        var later = OverlayContent.Build(new AppData { Timers = [Farm(paused)] }, All, Now.AddHours(5));
        Assert.Equal(TimeSpan.FromHours(-11), later.FarmLeft);
        Assert.Equal(150, later.FarmProgress);
    }

    [Fact]
    public void Pop_ups_inside_their_window_get_rows()
    {
        var soon = WithPopUp(TestTimers.Countdown(Now.AddMinutes(4), 0) with { Name = "Bread" }, 5);
        var later = WithPopUp(TestTimers.Countdown(Now.AddMinutes(9), 0) with { Name = "Later" }, 5);

        var s = OverlayContent.Build(new AppData { Timers = [soon, later] }, All with { ShowCustomTimers = false }, Now);

        Assert.Equal(new[] { "Bread" }, s.PopUps.Select(p => p.Timer.Name));
    }

    [Fact]
    public void A_muted_pop_up_gets_no_row()
    {
        var bread = WithPopUp(TestTimers.Countdown(Now.AddMinutes(4), 0) with { Name = "Bread" }, 5);
        var data = new AppData { Timers = [bread], Muted = [new MutedOccurrence(bread.Id, Now.AddMinutes(4))] };

        Assert.Empty(OverlayContent.Build(data, All, Now).PopUps);
    }

    [Fact]
    public void A_boss_shown_as_next_gets_no_pop_up_row()
    {
        var data = new AppData { Timers = [WithPopUp(Boss("Kzarka", 12, 3), 5)] };

        Assert.Empty(OverlayContent.Build(data, All, Now).PopUps);
        Assert.Single(OverlayContent.Build(data, All with { ShowNext = false }, Now).PopUps);
    }

    [Fact]
    public void Farm_gets_no_pop_up_row_while_its_own_row_shows()
    {
        var farm = WithPopUp(Farm(CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromMinutes(3) }, Now)), 5);
        var data = new AppData { Timers = [farm] };

        Assert.Empty(OverlayContent.Build(data, All, Now).PopUps);
        Assert.Single(OverlayContent.Build(data, All with { ShowFarm = false }, Now).PopUps);
    }

    [Fact]
    public void The_clock_alone_is_content()
    {
        var clockOnly = new OverlaySettings { ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false };

        Assert.False(OverlayContent.Build(new AppData(), clockOnly, Now).IsEmpty);
        Assert.True(OverlayContent.Build(new AppData(), clockOnly with { ShowClock = false }, Now).IsEmpty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deduplicated_pop_up_still_opens_overlay_and_hides_after_occurrence(bool boss)
    {
        var timer = WithPopUp(boss ? Boss("Kzarka", 12, 3)
            : Farm(CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromMinutes(3) }, Now)), 5);
        var data = new AppData { Timers = [timer] };
        var content = OverlayContent.Build(data, All, Now);

        Assert.Empty(content.PopUps);
        Assert.True(new OverlayPresence().IsVisible(All, Now, content, false));
        var later = Now.AddMinutes(4);
        Assert.False(new OverlayPresence().IsVisible(All, later, OverlayContent.Build(data, All, later), false));
        Assert.False(new OverlayPresence().IsVisible(All with { Enabled = false }, Now, content, false));
    }

    [Fact]
    public void Horse_section_shows_two_newest_running_registrations_and_counts_the_rest()
    {
        var runs = Enumerable.Range(1, 4).Select(i => new TimerDef
        {
            Name = $"Horse registration {i}", Kind = TimerKind.Countdown, Preset = Presets.HorseRegistrationRun,
            HorseRunNumber = i,
            Countdown = CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromMinutes(10) }, Now.AddSeconds(i)),
            Alerts = new AlertConfig { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 10 } },
        }).ToList();
        var data = new AppData { Timers = runs };
        var settings = All with { ShowHorseRegistrations = true };

        var snapshot = OverlayContent.Build(data, settings, Now.AddSeconds(5));

        Assert.Equal(["Horse 4", "Horse 3"], snapshot.HorseRegistrations.Select(r => r.Name));
        Assert.Equal([runs[3].Id, runs[2].Id], snapshot.HorseRegistrations.Select(r => r.Id));
        Assert.Equal(2, snapshot.MoreHorseRegistrations);
        Assert.Empty(snapshot.PopUps);
        Assert.Equal(4, OverlayContent.Build(data, settings with { ShowHorseRegistrations = false }, Now.AddSeconds(5)).PopUps.Count);
    }
}
