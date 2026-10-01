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
    public void Gate_agrees_with_the_due_pop_ups_as_time_moves_on()
    {
        var off = WithPopUp(TestTimers.Countdown(Now.AddMinutes(40), 0), 5) with { Enabled = false };
        var data = new AppData { Timers = [Bread, Kzarka, off], Muted = [new MutedOccurrence(Kzarka.Id, KzarkaToday)] };
        var gate = new OverlayPopUpGate();

        for (var t = Now; t < Now.AddDays(8); t = t.AddSeconds(30))
        {
            var due = UpcomingQuery.ForOverlay(data, t).Count > 0;
            Assert.True(due == gate.MayBeDue(data, t), $"{t:O}: due {due}");
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
