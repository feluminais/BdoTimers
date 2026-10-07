using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;
using static BdoTimers.Core.Tests.TestTimers;

namespace BdoTimers.Core.Tests;

public class GarmothTrackerTests
{
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    /// <summary>The third kill was marked on Tuesday at 10:00 Berlin; the week resets on Thursday at 00:00 UTC.</summary>
    static readonly GarmothWeek Done = new() { Kills = 3, DoneAtUtc = Utc(22, 8, 0), ResetUtc = Utc(24, 0, 0) };
    static readonly TodoSchedule Weekly = new();

    static TimerDef Garmoth(DayOfWeek day, int hour, int minute = 0) => Boss("Garmoth", day, hour, minute);

    [Fact]
    public void Garmoth_spawns_from_the_third_kill_until_the_reset_are_gone()
    {
        var garmoth = Garmoth(DayOfWeek.Tuesday, 14);
        var data = new AppData { Timers = [garmoth], Garmoth = Done };

        Assert.False(GarmothTracker.Gone(data, garmoth, Done.DoneAtUtc!.Value.AddTicks(-1)));
        Assert.True(GarmothTracker.Gone(data, garmoth, Done.DoneAtUtc!.Value));
        Assert.True(GarmothTracker.Gone(data, garmoth, Done.ResetUtc.AddTicks(-1)));
        Assert.False(GarmothTracker.Gone(data, garmoth, Done.ResetUtc));
    }

    [Fact]
    public void Only_the_built_in_Garmoth_is_ever_gone_and_not_before_the_third_kill()
    {
        var garmoth = Garmoth(DayOfWeek.Tuesday, 14);
        var kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 14);
        var added = garmoth with { AddedByUser = true };
        var custom = TestTimers.Scheduled("Garmoth", DayOfWeek.Tuesday, 14, 0, 0);
        var at = Utc(22, 12, 0);
        var done = new AppData { Timers = [garmoth, kzarka, added, custom], Garmoth = Done };

        Assert.True(GarmothTracker.Gone(done, garmoth, at));
        Assert.False(GarmothTracker.Gone(done, kzarka, at));
        Assert.False(GarmothTracker.Gone(done, added, at));
        Assert.False(GarmothTracker.Gone(done, custom, at));
        Assert.False(GarmothTracker.Gone(done with { Garmoth = Done with { Kills = 2, DoneAtUtc = null } }, garmoth, at));
        Assert.False(GarmothTracker.Gone(done with { Garmoth = new() }, garmoth, at));
    }

    [Theory]
    [InlineData(0, 1, 1, false)]
    [InlineData(0, 2, 2, false)]
    [InlineData(1, 3, 3, true)]
    [InlineData(0, 3, 3, true)]
    [InlineData(2, 3, 3, true)]
    [InlineData(1, 1, 0, false)]
    [InlineData(2, 2, 1, false)]
    [InlineData(3, 3, 2, false)]
    [InlineData(3, 2, 1, false)]
    [InlineData(3, 1, 0, false)]
    public void Pressing_a_kill_marks_it_and_the_ones_before_it_and_pressing_a_marked_one_takes_it_and_the_rest_back(
        int kills, int press, int expected, bool done)
    {
        var week = GarmothTracker.Mark(new GarmothWeek { Kills = kills, ResetUtc = Utc(24, 0, 0) }, press, BerlinNoon);

        Assert.Equal(expected, week.Kills);
        Assert.Equal(done ? (DateTimeOffset?)BerlinNoon : null, week.DoneAtUtc);
        Assert.Equal(Utc(24, 0, 0), week.ResetUtc);
    }

    [Fact]
    public void Garmoth_is_gone_from_the_moment_the_third_kill_is_marked_not_from_a_later_press()
    {
        var third = GarmothTracker.Mark(new GarmothWeek { Kills = 2, ResetUtc = Utc(24, 0, 0) }, 3, BerlinNoon);

        Assert.Equal(BerlinNoon, third.DoneAtUtc);
        Assert.Null(GarmothTracker.Mark(third, 3, BerlinNoon.AddHours(1)).DoneAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void A_kill_outside_one_to_three_is_refused(int press) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GarmothTracker.Mark(new(), press, BerlinNoon));

    [Fact]
    public void With_the_tracker_off_the_week_is_cleared()
    {
        Assert.Equal(new GarmothWeek(), GarmothTracker.Reconcile(Done, tracking: false, Weekly, BerlinNoon));
    }

    [Fact]
    public void A_week_starts_when_the_tracker_goes_on_and_ends_at_the_weekly_reset()
    {
        var week = GarmothTracker.Reconcile(new(), tracking: true, Weekly, BerlinNoon);

        Assert.Equal(new GarmothWeek { ResetUtc = Utc(24, 0, 0) }, week);
        Assert.Equal(week, GarmothTracker.Reconcile(week, tracking: true, Weekly, BerlinNoon.AddHours(5)));
    }

    [Fact]
    public void The_kills_clear_when_the_reset_passes_and_the_next_week_starts()
    {
        var week = GarmothTracker.Reconcile(Done, tracking: true, Weekly, Utc(24, 0, 0));

        Assert.Equal(new GarmothWeek { ResetUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) }, week);
    }

    [Fact]
    public void A_changed_weekly_reset_moves_the_end_of_the_week_and_keeps_the_kills()
    {
        var wednesday = new TodoSchedule { Day = DayOfWeek.Wednesday, Hour = 12 };

        var week = GarmothTracker.Reconcile(Done, tracking: true, wednesday, BerlinNoon);

        Assert.Equal(3, week.Kills);
        Assert.Equal(Done.DoneAtUtc, week.DoneAtUtc);
        Assert.Equal(Utc(23, 12, 0), week.ResetUtc);
    }

    [Fact]
    public void Garmoth_who_is_done_is_off_the_board_but_is_still_the_previous_spawn_and_returns_after_the_reset()
    {
        var garmoth = Garmoth(DayOfWeek.Tuesday, 14);
        var kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 19);
        var data = new AppData { Timers = [garmoth, kzarka], Garmoth = Done };

        var board = BossBoard.Build(data, BerlinNoon);
        var open = BossBoard.Build(data with { Garmoth = new() }, BerlinNoon);
        var afterReset = BossBoard.Build(data, Utc(24, 1, 0));

        Assert.Equal("Kzarka", board.Next!.Bosses.Single().Name);
        Assert.Equal("Garmoth", open.Next!.Bosses.Single().Name);
        Assert.Equal("Garmoth", afterReset.Next!.Bosses.Single().Name);
        Assert.Equal(Utc(29, 12, 0), afterReset.Next.AtUtc);

        // Marked after the spawn that was just killed: that one stays as the previous spawn.
        var justDone = data with { Garmoth = Done with { DoneAtUtc = Utc(22, 12, 30) } };
        var justKilled = BossBoard.Build(justDone, Utc(22, 12, 31));
        Assert.Equal("Garmoth", justKilled.Previous!.Bosses.Single().Name);
        Assert.Equal("Kzarka", justKilled.Next!.Bosses.Single().Name);
    }

    [Fact]
    public void Garmoth_who_is_done_shows_grey_in_the_schedule_and_is_left_out_of_Coming_up()
    {
        var garmoth = Garmoth(DayOfWeek.Wednesday, 14);
        var kzarka = Boss("Kzarka", DayOfWeek.Wednesday, 19);
        var data = new AppData { Timers = [garmoth, kzarka], Garmoth = Done };
        var wednesdayMorning = Utc(23, 8, 0);

        var grid = WeekGrid.Build(data, wednesdayMorning, Berlin);
        var states = grid.Rows.SelectMany(r => r.Days).SelectMany(d => d).ToDictionary(e => e.Boss.Name, e => e.State);
        var comingUp = ComingUp.Between(data, new AppSettings(), wednesdayMorning, Berlin);

        Assert.Equal(CellState.Unfollowed, states["Garmoth"]);
        Assert.Equal(CellState.Next, states["Kzarka"]);
        Assert.Equal(new[] { "Kzarka" }, comingUp.Where(i => i.Kind == CalendarKind.Boss).Select(i => i.Timer!.Name));
        Assert.Contains(CalendarQuery.Between(data, new AppSettings(), wednesdayMorning, wednesdayMorning.AddDays(2), wednesdayMorning, Berlin),
            i => i.Timer == garmoth && i.State == CellState.Unfollowed);
    }

    [Fact]
    public void A_queued_alert_for_a_Garmoth_that_is_gone_is_dropped()
    {
        var garmoth = Garmoth(DayOfWeek.Tuesday, 14);
        var alert = new AlertEvent([garmoth], Utc(22, 12, 0), 5, 5) { BossSelectionVersion = Guid.Empty };

        Assert.Same(alert, AlertEligibility.Filter(new AppData { Timers = [garmoth] }, alert));
        Assert.Null(AlertEligibility.Filter(new AppData { Timers = [garmoth], Garmoth = Done }, alert));
    }

    [Fact]
    public void Silenced_adds_the_gone_spawns_to_the_skipped_ones()
    {
        var garmoth = Boss("Garmoth", (DayOfWeek.Tuesday, 14, 0), (DayOfWeek.Wednesday, 14, 0), (DayOfWeek.Thursday, 14, 0));
        var kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 14);
        var skipped = new MutedOccurrence(kzarka.Id, Utc(22, 12, 0));
        var data = new AppData { Timers = [garmoth, kzarka], Muted = [skipped], Garmoth = Done };

        var silenced = GarmothTracker.Silenced(data);

        // Tuesday and Wednesday are before the reset on Thursday 00:00 UTC; Thursday's is after it.
        Assert.Equal(new HashSet<MutedOccurrence> { skipped, new(garmoth.Id, Utc(22, 12, 0)), new(garmoth.Id, Utc(23, 12, 0)) }, silenced);
        Assert.Equal(new HashSet<MutedOccurrence> { skipped }, GarmothTracker.Silenced(data with { Garmoth = new() }));
    }

    [Fact]
    public void The_store_saves_a_marked_kill_and_leaves_a_week_that_is_as_it_should_be_alone()
    {
        using var dir = new TempDir();
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new());
        var changes = 0;
        store.Changed += () => changes++;
        var on = new AppSettings { GarmothTracker = true };

        store.ReconcileGarmoth(on, BerlinNoon);
        store.ReconcileGarmoth(on, BerlinNoon.AddMinutes(1));
        store.MarkGarmoth(2, BerlinNoon);
        store.ReconcileGarmoth(on, BerlinNoon.AddMinutes(2));

        Assert.Equal(new GarmothWeek { Kills = 2, ResetUtc = Utc(24, 0, 0) }, store.Current.Garmoth);
        Assert.Equal(2, changes);

        store.ReconcileGarmoth(new AppSettings(), BerlinNoon);

        Assert.Equal(new GarmothWeek(), store.Current.Garmoth);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void The_overlay_has_no_pop_up_for_a_Garmoth_that_is_gone()
    {
        var overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 10 };
        var garmoth = Garmoth(DayOfWeek.Tuesday, 12, 8);
        garmoth = garmoth with { Alerts = garmoth.Alerts with { Overlay = overlay } };
        var data = new AppData { Timers = [garmoth], Garmoth = Done };

        Assert.Single(UpcomingQuery.ForOverlay(data with { Garmoth = new() }, new OverlaySettings(), BerlinNoon));
        Assert.Empty(UpcomingQuery.ForOverlay(data, new OverlaySettings(), BerlinNoon));
    }
}
