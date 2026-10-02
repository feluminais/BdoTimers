using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimers;

namespace BdoTimers.Core.Tests;

public class WeekGridTests
{
    // 2026-09-22 is a Tuesday; Berlin is UTC+2 until 2026-10-25.
    static readonly DateTimeOffset TuesdayThreePmBerlin = new(2026, 9, 22, 13, 0, 0, TimeSpan.Zero);
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    static readonly TimerDef Kzarka = Boss("Kzarka", (DayOfWeek.Monday, 22, 15), (DayOfWeek.Tuesday, 19, 0));
    static readonly TimerDef Nouver = Boss("Nouver", (DayOfWeek.Tuesday, 16, 0));
    static readonly TimerDef Garmoth = Boss("Garmoth", (DayOfWeek.Tuesday, 14, 0));
    static readonly TimerDef Uturi = Boss("Uturi", (DayOfWeek.Tuesday, 19, 0)) with { Enabled = false };

    static GridEntry Entry(WeekGridState grid, string time, int day, string name) =>
        grid.Rows.Single(r => r.Time == TimeOnly.Parse(time)).Days[day].Single(e => e.Boss.Name == name);

    [Fact]
    public void Rows_are_the_distinct_local_times_of_the_week()
    {
        var grid = WeekGrid.Build(new AppData { Timers = [Kzarka, Nouver, Garmoth, Uturi] }, TuesdayThreePmBerlin, Berlin);

        Assert.Equal(new DateOnly(2026, 9, 21), grid.WeekStart);
        Assert.Equal(new[] { "14:00", "16:00", "19:00", "22:15" }, grid.Rows.Select(r => r.Time.ToString("HH:mm")));
        Assert.All(grid.Rows, r => Assert.Equal(7, r.Days.Count));
        Assert.Equal(new[] { "Kzarka", "Uturi" }, grid.Rows[2].Days[1].Select(e => e.Boss.Name));
    }

    [Fact]
    public void Cells_carry_past_next_upcoming_and_unfollowed_states()
    {
        var grid = WeekGrid.Build(new AppData { Timers = [Kzarka, Nouver, Garmoth, Uturi] }, TuesdayThreePmBerlin, Berlin);

        Assert.Equal(CellState.Past, Entry(grid, "22:15", 0, "Kzarka").State);
        Assert.Equal(CellState.Past, Entry(grid, "14:00", 1, "Garmoth").State);
        Assert.Equal(CellState.Next, Entry(grid, "16:00", 1, "Nouver").State);
        Assert.Equal(CellState.Upcoming, Entry(grid, "19:00", 1, "Kzarka").State);
        Assert.Equal(CellState.Unfollowed, Entry(grid, "19:00", 1, "Uturi").State);
    }

    [Fact]
    public void Muted_spawns_are_skipped()
    {
        var at = new DateTimeOffset(2026, 9, 22, 14, 0, 0, TimeSpan.Zero);
        var data = new AppData { Timers = [Nouver, Kzarka], Muted = [new MutedOccurrence(Nouver.Id, at)] };

        var grid = WeekGrid.Build(data, TuesdayThreePmBerlin, Berlin);

        Assert.Equal(CellState.Skipped, Entry(grid, "16:00", 1, "Nouver").State);
        Assert.Equal(at, Entry(grid, "16:00", 1, "Nouver").AtUtc);
    }

    [Fact]
    public void Rows_follow_the_local_time_zone()
    {
        var kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

        var grid = WeekGrid.Build(new AppData { Timers = [Garmoth] }, TuesdayThreePmBerlin, kyiv);

        Assert.Equal(new TimeOnly(15, 0), grid.Rows.Single().Time);
        Assert.Single(grid.Rows.Single().Days[1]);
    }

    [Fact]
    public void Week_starts_on_monday_when_today_is_sunday()
    {
        var sundayNoonBerlin = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        var grid = WeekGrid.Build(new AppData { Timers = [Garmoth] }, sundayNoonBerlin, Berlin);

        Assert.Equal(new DateOnly(2026, 9, 21), grid.WeekStart);
        Assert.Equal(CellState.Past, Entry(grid, "14:00", 1, "Garmoth").State);
    }

    [Fact]
    public void Daylight_saving_week_keeps_local_rows_and_correct_instants()
    {
        var garmoth = Boss("Garmoth", (DayOfWeek.Monday, 14, 0), (DayOfWeek.Sunday, 14, 0));
        var sundayOfTheChange = new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

        var grid = WeekGrid.Build(new AppData { Timers = [garmoth] }, sundayOfTheChange, Berlin);

        var row = grid.Rows.Single();
        Assert.Equal(new TimeOnly(14, 0), row.Time);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), row.Days[0].Single().AtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 13, 0, 0, TimeSpan.Zero), row.Days[6].Single().AtUtc);
    }
}
