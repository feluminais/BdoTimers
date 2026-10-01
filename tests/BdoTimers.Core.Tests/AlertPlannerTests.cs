using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

[Collection(nameof(Log))]
public class AlertPlannerTests
{
    static readonly DateTimeOffset T = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    static readonly HashSet<MutedOccurrence> NoMutes = [];

    [Fact]
    public void Fires_each_lead_once_as_time_passes()
    {
        var timer = TestTimers.Countdown(T, 5, 1, 0);
        var planner = new AlertPlanner();

        Assert.Empty(planner.Tick([timer], NoMutes, T.AddMinutes(-6)));

        var five = planner.Tick([timer], NoMutes, T.AddMinutes(-5));
        Assert.Equal(5, five.Single().LeadMinutes);
        Assert.Equal(5, five.Single().MinutesLeft);

        Assert.Empty(planner.Tick([timer], NoMutes, T.AddMinutes(-5).AddSeconds(1)));

        Assert.Equal(1, planner.Tick([timer], NoMutes, T.AddMinutes(-1)).Single().LeadMinutes);

        var now = planner.Tick([timer], NoMutes, T).Single();
        Assert.Equal(0, now.LeadMinutes);
        Assert.Equal(0, now.MinutesLeft);

        Assert.Empty(planner.Tick([timer], NoMutes, T.AddSeconds(1)));
    }

    [Fact]
    public void After_a_gap_only_the_most_recent_due_lead_fires()
    {
        var timer = TestTimers.Countdown(T, 15, 5, 1, 0);
        var planner = new AlertPlanner();

        var first = planner.Tick([timer], NoMutes, T.AddMinutes(-2));
        Assert.Equal(5, first.Single().LeadMinutes);
        Assert.Equal(2, first.Single().MinutesLeft);

        Assert.Empty(planner.Tick([timer], NoMutes, T.AddMinutes(-2).AddSeconds(1)));
        Assert.Equal(1, planner.Tick([timer], NoMutes, T.AddMinutes(-1)).Single().LeadMinutes);
    }

    [Fact]
    public void Occurrence_older_than_grace_is_skipped()
    {
        var timer = TestTimers.Countdown(T, 5, 0);
        Assert.Empty(new AlertPlanner().Tick([timer], NoMutes, T.AddMinutes(2)));
    }

    [Fact]
    public void Occurrence_within_grace_still_fires_now_alert()
    {
        var timer = TestTimers.Countdown(T, 5, 0);
        var alert = new AlertPlanner().Tick([timer], NoMutes, T.AddSeconds(30)).Single();
        Assert.Equal(0, alert.LeadMinutes);
    }

    [Fact]
    public void Muted_occurrence_does_not_fire()
    {
        var timer = TestTimers.Countdown(T, 5, 0);
        var muted = new HashSet<MutedOccurrence> { new(timer.Id, T) };
        Assert.Empty(new AlertPlanner().Tick([timer], muted, T.AddMinutes(-5)));
    }

    [Fact]
    public void Disabled_timer_does_not_fire()
    {
        var timer = TestTimers.Countdown(T, 5, 0) with { Enabled = false };
        Assert.Empty(new AlertPlanner().Tick([timer], NoMutes, T.AddMinutes(-5)));
    }

    [Fact]
    public void Scheduled_timer_fires_at_slot()
    {
        // Tuesday 14:00 Berlin = 12:00Z on 2026-09-22.
        var timer = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 0);
        var alert = new AlertPlanner().Tick([timer], NoMutes, T).Single();
        Assert.Equal(T, alert.OccurrenceUtc);
        Assert.Equal("Kzarka", alert.Timers.Single().Name);
    }

    [Fact]
    public void Idle_countdown_has_no_occurrences()
    {
        var timer = TestTimers.Countdown(T, 0) with { Countdown = new CountdownSpec() };
        Assert.Empty(new AlertPlanner().Tick([timer], NoMutes, T));
    }

    static List<AlertEvent> Run(AlertPlanner planner, TimerDef timer, DateTimeOffset from, DateTimeOffset to)
    {
        var events = new List<AlertEvent>();
        for (var at = from; at <= to; at = at.AddSeconds(10)) events.AddRange(planner.Tick([timer], NoMutes, at));
        return events;
    }

    [Fact]
    public void Setting_the_clock_back_doesnt_replay_alerts()
    {
        var timer = TestTimers.Countdown(T, 5, 1, 0);
        var planner = new AlertPlanner();
        Assert.Equal(new[] { 5, 1, 0 }, Run(planner, timer, T.AddMinutes(-6), T.AddMinutes(30)).Select(a => a.LeadMinutes));

        Assert.Empty(Run(planner, timer, T.AddMinutes(-6), T.AddMinutes(1)));
    }

    [Fact]
    public void Setting_the_clock_back_further_than_the_planner_remembers_alerts_again()
    {
        var timer = TestTimers.Countdown(T, 0);
        var planner = new AlertPlanner();
        Run(planner, timer, T, T);
        Run(planner, timer, T + AlertPlanner.Memory + AlertPlanner.Grace, T + AlertPlanner.Memory + AlertPlanner.Grace);

        Assert.Single(Run(planner, timer, T, T));
    }

    [Fact]
    public void A_timer_that_cant_be_scheduled_doesnt_stop_the_others()
    {
        var broken = TestTimers.Scheduled("Broken", DayOfWeek.Tuesday, 14, 0, 0);
        broken = broken with { Scheduled = broken.Scheduled! with { TimeZoneId = "Nowhere/Nothing" } };
        var kzarka = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 0);

        var alert = new AlertPlanner().Tick([broken, kzarka], NoMutes, T).Single();

        Assert.Equal("Kzarka", alert.Timers.Single().Name);
    }

    [Fact]
    public void A_timer_that_cant_be_scheduled_is_logged_once()
    {
        using var dir = new TempDir();
        Log.Init(dir.Path, new FakeClock(T));
        var broken = TestTimers.Scheduled("Unschedulable", DayOfWeek.Tuesday, 14, 0, 0);
        broken = broken with { Scheduled = broken.Scheduled! with { TimeZoneId = "Nowhere/Nothing" } };
        var planner = new AlertPlanner();

        planner.Tick([broken], NoMutes, T);
        planner.Tick([broken], NoMutes, T.AddSeconds(1));

        var log = File.ReadAllLines(Directory.GetFiles(dir.Path, "*.log").Single());
        Assert.Single(log, line => line.Contains("Unschedulable"));
    }
}
