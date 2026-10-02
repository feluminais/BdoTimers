using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class OverlayPopUpGateTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero); // Tue 12:00 Berlin
    static readonly DateTimeOffset KzarkaToday = new(2026, 9, 22, 17, 0, 0, TimeSpan.Zero);

    static TimerDef WithPopUp(TimerDef t, int minutes) =>
        t with { Alerts = t.Alerts with { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = minutes } } };

    static readonly TimerDef Bread = WithPopUp(TestTimers.Countdown(Now.AddMinutes(20), 0) with { Name = "Bread" }, 5);
    static readonly TimerDef Kzarka = WithPopUp(TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 19, 0, 0), 10);

    [Fact]
    public void Nothing_starts_without_an_opted_in_timer()
    {
        var data = new AppData { Timers = [TestTimers.Countdown(Now.AddMinutes(3), 0)] };

        Assert.Null(UpcomingQuery.OverlayStart(data, Now));
    }

    [Fact]
    public void Starts_where_the_first_unmuted_occurrence_enters_its_window()
    {
        var data = new AppData { Timers = [Bread, Kzarka] };
        Assert.Equal(Now.AddMinutes(15), UpcomingQuery.OverlayStart(data, Now));

        var breadMuted = data with { Muted = [new MutedOccurrence(Bread.Id, Now.AddMinutes(20))] };
        Assert.Equal(KzarkaToday.AddMinutes(-10), UpcomingQuery.OverlayStart(breadMuted, Now));
    }

    [Fact]
    public void A_due_pop_up_has_already_started()
    {
        var data = new AppData { Timers = [WithPopUp(TestTimers.Countdown(Now.AddMinutes(3), 0), 5)] };

        Assert.True(UpcomingQuery.OverlayStart(data, Now) <= Now);
    }

    [Fact]
    public void Gate_opens_and_closes_at_window_boundaries_and_skips_muted_and_disabled_occurrences()
    {
        var off = WithPopUp(TestTimers.Countdown(Now.AddMinutes(40), 0), 5) with { Enabled = false };
        var data = new AppData { Timers = [Bread, Kzarka, off], Muted = [new MutedOccurrence(Kzarka.Id, KzarkaToday)] };
        var gate = new OverlayPopUpGate();
        var nextWeek = KzarkaToday.AddDays(7);
        (DateTimeOffset At, bool Due)[] cases =
        [
            (Now, false),
            (Now.AddMinutes(15).AddTicks(-1), false),
            (Now.AddMinutes(15), true),
            (Now.AddMinutes(20).AddTicks(-1), true),
            (Now.AddMinutes(20), true),
            (Now.AddMinutes(20).AddTicks(1), false),
            (Now.AddMinutes(35), false),
            (Now.AddMinutes(40), false),
            (KzarkaToday.AddMinutes(-10), false),
            (KzarkaToday, false),
            (KzarkaToday.AddTicks(1), false),
            (nextWeek.AddMinutes(-10).AddTicks(-1), false),
            (nextWeek.AddMinutes(-10), true),
            (nextWeek, true),
            (nextWeek.AddTicks(1), false),
        ];

        foreach (var (at, due) in cases)
        {
            Assert.True(due == gate.MayBeDue(data, at), $"{at:O}: gate should be {due}");
            Assert.True(due == (UpcomingQuery.ForOverlay(data, at).Count > 0), $"{at:O}: pop-up should be {due}");
        }
    }

    [Fact]
    public void Gate_reads_a_new_data_instance_at_once()
    {
        var data = new AppData { Timers = [Bread] };
        var gate = new OverlayPopUpGate();
        Assert.False(gate.MayBeDue(data, Now));

        var started = data with { Timers = [Bread, WithPopUp(TestTimers.Countdown(Now.AddMinutes(3), 0), 5)] };

        Assert.True(gate.MayBeDue(started, Now));
    }

    [Fact]
    public void Gate_looks_again_when_the_clock_is_set_back()
    {
        var data = new AppData { Timers = [WithPopUp(TestTimers.Countdown(Now.AddMinutes(3), 0), 5)] };
        var gate = new OverlayPopUpGate();
        Assert.False(gate.MayBeDue(data, Now.AddMinutes(10)));

        Assert.True(gate.MayBeDue(data, Now));
    }
}
