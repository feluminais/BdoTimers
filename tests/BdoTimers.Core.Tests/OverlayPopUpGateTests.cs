using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class OverlayPopUpGateTests
{
    static readonly DateTimeOffset KzarkaToday = new(2026, 9, 22, 17, 0, 0, TimeSpan.Zero);

    static TimerDef WithPopUp(TimerDef t, int minutes) =>
        t with { Alerts = t.Alerts with { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = minutes } } };

    static readonly TimerDef Bread = WithPopUp(TestTimers.Countdown(BerlinNoon.AddMinutes(20), 0) with { Name = "Bread" }, 5);
    static readonly TimerDef Kzarka = WithPopUp(TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 19, 0, 0), 10);

    [Fact]
    public void Nothing_starts_without_an_opted_in_timer()
    {
        var data = new AppData { Timers = [TestTimers.Countdown(BerlinNoon.AddMinutes(3), 0)] };

        Assert.Null(UpcomingQuery.OverlayStart(data, BerlinNoon));
    }

    [Fact]
    public void Starts_where_the_first_unmuted_occurrence_enters_its_window()
    {
        var data = new AppData { Timers = [Bread, Kzarka] };
        Assert.Equal(BerlinNoon.AddMinutes(15), UpcomingQuery.OverlayStart(data, BerlinNoon));

        var breadMuted = data with { Muted = [new MutedOccurrence(Bread.Id, BerlinNoon.AddMinutes(20))] };
        Assert.Equal(KzarkaToday.AddMinutes(-10), UpcomingQuery.OverlayStart(breadMuted, BerlinNoon));
    }

    [Fact]
    public void A_due_pop_up_has_already_started()
    {
        var data = new AppData { Timers = [WithPopUp(TestTimers.Countdown(BerlinNoon.AddMinutes(3), 0), 5)] };

        Assert.True(UpcomingQuery.OverlayStart(data, BerlinNoon) <= BerlinNoon);
    }

    [Fact]
    public void Gate_agrees_with_the_due_pop_ups_as_time_moves_on()
    {
        var off = WithPopUp(TestTimers.Countdown(BerlinNoon.AddMinutes(40), 0), 5) with { Enabled = false };
        var data = new AppData { Timers = [Bread, Kzarka, off], Muted = [new MutedOccurrence(Kzarka.Id, KzarkaToday)] };
        var gate = new OverlayPopUpGate();

        for (var t = BerlinNoon; t < BerlinNoon.AddDays(8); t = t.AddSeconds(30))
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
        Assert.False(gate.MayBeDue(data, BerlinNoon));

        var started = data with { Timers = [Bread, WithPopUp(TestTimers.Countdown(BerlinNoon.AddMinutes(3), 0), 5)] };

        Assert.True(gate.MayBeDue(started, BerlinNoon));
    }

    [Fact]
    public void Gate_looks_again_when_the_clock_is_set_back()
    {
        var data = new AppData { Timers = [WithPopUp(TestTimers.Countdown(BerlinNoon.AddMinutes(3), 0), 5)] };
        var gate = new OverlayPopUpGate();
        Assert.False(gate.MayBeDue(data, BerlinNoon.AddMinutes(10)));

        Assert.True(gate.MayBeDue(data, BerlinNoon));
    }
}
