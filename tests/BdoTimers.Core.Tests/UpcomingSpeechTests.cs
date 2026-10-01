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

    [Fact]
    public void Nothing_is_prepared_until_the_next_spoken_alert_is_within_the_window()
    {
        Assert.Empty(new AlertPlanner().UpcomingSpeech([Boss("Kzarka", 15, 5, 1, 0)], NoMutes, T.AddMinutes(-16.5), Window));
    }

    [Fact]
    public void The_first_alert_of_an_occurrence_prepares_all_of_its_speech()
    {
        var speech = new AlertPlanner().UpcomingSpeech([Boss("Kzarka", 15, 5, 1, 0)], NoMutes, T.AddMinutes(-15.5), Window);

        Assert.Equal(["Kzarka in 15 minutes", "Kzarka in 5 minutes", "Kzarka in 1 minute", "Kzarka now"], speech);
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
        var start = T.AddMinutes(-30.5);

        var prepared = new List<string>();
        var spoken = new List<string>();
        for (var at = start; at <= T; at = at.AddSeconds(1))
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
