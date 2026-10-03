using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;

namespace BdoTimers.Core.Tests;

// A timer that can't be scheduled is logged.
[Collection(nameof(Log))]
public class UpcomingSpeechTests
{
    // Tuesday 14:00 Berlin.
    static readonly DateTimeOffset T = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    static readonly HashSet<MutedOccurrence> NoMutes = [];
    static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    static TimerDef Boss(string name, params int[] leads) => TestTimers.Scheduled(name, DayOfWeek.Tuesday, 14, 0, leads);

    static TimerDef Silent(TimerDef timer) => timer with { Alerts = timer.Alerts with { Tts = new TtsAlert { Enabled = false } } };

    [Theory]
    [InlineData("Sangoon", "Karanda")]
    [InlineData("Golden Pig King", "Nouver")]
    [InlineData("Bulgasal", "Kzarka")]
    [InlineData("Uturi", "Kutum")]
    public void Morning_Light_leads_prepared_and_spoken_shared_alerts(string morningLight, string other)
    {
        var timers = new[] { Boss(other, 0) with { IsBuiltIn = true }, Boss(morningLight, 0) with { IsBuiltIn = true } };
        var planner = new AlertPlanner();

        Assert.Equal([$"{morningLight} and {other} now"],
            planner.UpcomingSpeech(timers, NoMutes, T.AddSeconds(-30), Window));
        var alert = AlertGrouping.Group(planner.Tick(timers, NoMutes, T)).Single();
        Assert.Equal($"{morningLight} and {other} now", AlertMessage.Build(alert).Speech);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void The_preparation_window_includes_its_boundary_and_prepares_the_whole_occurrence(int ticks, bool withinWindow)
    {
        var now = T.AddMinutes(-16).AddTicks(ticks);
        var speech = new AlertPlanner().UpcomingSpeech([Boss("Kzarka", 15, 5, 1, 0)], NoMutes, now, Window);

        if (withinWindow)
            Assert.Equal(["Kzarka in 15 minutes", "Kzarka in 5 minutes", "Kzarka in 1 minute", "Kzarka now"], speech);
        else
            Assert.Empty(speech);
    }

    [Fact]
    public void Alerts_already_due_are_left_out()
    {
        var kzarka = Boss("Kzarka", 15, 5, 1, 0);
        var planner = new AlertPlanner();

        Assert.Empty(planner.UpcomingSpeech([kzarka], NoMutes, T.AddMinutes(-10), Window));
        Assert.Equal(["Kzarka in 5 minutes", "Kzarka in 1 minute", "Kzarka now"],
            planner.UpcomingSpeech([kzarka], NoMutes, T.AddMinutes(-5.5), Window));
    }

    [Fact]
    public void Prepared_speech_is_what_the_alerts_say()
    {
        // Each speaks at different times; Nouver's voice is off but its name joins the line they share.
        var timers = new[] { Boss("Kzarka", 15, 5, 1, 0), Silent(Boss("Nouver", 5, 0)), Boss("Karanda", 30, 0) };
        var planner = new AlertPlanner();
        DateTimeOffset[] ticks = [T.AddMinutes(-30.5), T.AddMinutes(-30), T.AddMinutes(-15), T.AddMinutes(-5), T.AddMinutes(-1), T];

        var prepared = new List<string>();
        var spoken = new List<string>();
        foreach (var at in ticks)
        {
            spoken.AddRange(AlertGrouping.Group(planner.Tick(timers, NoMutes, at))
                .Where(a => a.Timers.Any(t => t.Alerts.Tts.Enabled))
                .Select(a => AlertMessage.Build(a).Speech));
            prepared.AddRange(planner.UpcomingSpeech(timers, NoMutes, at, Window).Except(prepared));
        }

        Assert.Equal(
            ["Karanda in 30 minutes", "Kzarka in 15 minutes", "Kzarka and Nouver in 5 minutes", "Kzarka in 1 minute",
                "Karanda, Kzarka and Nouver now"],
            spoken);
        Assert.Equal(spoken, prepared);
    }

    [Fact]
    public void Voice_off_disabled_and_muted_timers_prepare_nothing()
    {
        var silent = Silent(Boss("Kzarka", 0));
        var disabled = Boss("Nouver", 0) with { Enabled = false };
        var muted = Boss("Karanda", 0);
        var mutes = new HashSet<MutedOccurrence> { new(muted.Id, T) };

        Assert.Empty(new AlertPlanner().UpcomingSpeech([silent, disabled, muted], mutes, T.AddSeconds(-30), Window));
    }

    [Fact]
    public void A_timer_that_cant_be_scheduled_doesnt_stop_the_others()
    {
        var broken = Boss("Broken", 0);
        broken = broken with { Scheduled = broken.Scheduled! with { TimeZoneId = "Nowhere/Nothing" } };

        var speech = new AlertPlanner().UpcomingSpeech([broken, Boss("Kzarka", 0)], NoMutes, T.AddSeconds(-30), Window);

        Assert.Equal(["Kzarka now"], speech);
    }
}
