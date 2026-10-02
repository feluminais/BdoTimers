using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class BossBoardCacheTests
{
    // 2026-09-22 is a Tuesday; Berlin is UTC+2 until 2026-10-25.
    static readonly DateTimeOffset TuesdayNoonBerlin = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

    static TimerDef Boss(string name, DayOfWeek day, int hour, int minute) =>
        TestTimers.Scheduled(name, day, hour, minute, 0) with { IsBuiltIn = true };

    static DateTimeOffset Utc(int d, int h, int m) => new(2026, 9, d, h, m, 0, TimeSpan.Zero);

    static readonly TimerDef Nouver = Boss("Nouver", DayOfWeek.Tuesday, 16, 0);
    static readonly TimerDef Kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 19, 0);
    static readonly TimerDef Uturi = Boss("Uturi", DayOfWeek.Tuesday, 19, 0);
    static readonly TimerDef Garmoth = Boss("Garmoth", DayOfWeek.Thursday, 23, 15);

    static void AssertSameBoard(BossBoardState expected, BossBoardState actual)
    {
        static void AssertGroup(SpawnGroup? expected, SpawnGroup? actual)
        {
            if (expected is null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(expected.AtUtc, actual.AtUtc);
            Assert.Equal(expected.Skipped, actual.Skipped);
            Assert.Equal(expected.Bosses, actual.Bosses);
        }

        AssertGroup(expected.Previous, actual.Previous);
        AssertGroup(expected.Next, actual.Next);
        AssertGroup(expected.FollowedBy, actual.FollowedBy);
    }

    public static TheoryData<string> Timetables => new() { "one boss", "several" };

    static AppData Data(string timetable) => timetable == "one boss"
        // A single weekly boss: the spawn after next comes within reach only a day before the next one.
        ? new AppData { Timers = [Kzarka] }
        : new AppData
        {
            Timers = [Nouver, Kzarka, Uturi, Garmoth],
            Muted = [new MutedOccurrence(Nouver.Id, Utc(22, 14, 0))],
        };

    [Theory]
    [MemberData(nameof(Timetables))]
    public void Matches_a_fresh_board_at_spawn_reach_and_daily_refresh_boundaries(string timetable)
    {
        var data = Data(timetable);
        var cache = new BossBoardCache();
        DateTimeOffset[] boundaries =
        [
            Utc(22, 14, 0), Utc(22, 17, 0), Utc(23, 10, 0), Utc(24, 10, 0), Utc(24, 21, 15),
            Utc(28, 17, 0), Utc(29, 14, 0), Utc(29, 17, 0), Utc(30, 10, 0),
            new(2026, 10, 1, 21, 15, 0, TimeSpan.Zero),
        ];
        var times = new[] { TuesdayNoonBerlin }
            .Concat(boundaries.SelectMany(at => new[] { at.AddTicks(-1), at, at.AddTicks(1) }))
            .Order();

        foreach (var now in times) AssertSameBoard(BossBoard.Build(data, now), cache.Get(data, now));
    }

    [Fact]
    public void Following_week_enters_reach_only_after_the_boundary()
    {
        var data = Data("one boss");
        var cache = new BossBoardCache();
        cache.Get(data, TuesdayNoonBerlin);
        var boundary = Utc(28, 17, 0);

        var before = cache.Get(data, boundary.AddTicks(-1));
        var at = cache.Get(data, boundary);
        Assert.Same(before, at);
        Assert.Equal(Utc(22, 17, 0), at.Previous!.AtUtc);
        Assert.Equal(Utc(29, 17, 0), at.Next!.AtUtc);
        Assert.Null(at.FollowedBy);

        var after = cache.Get(data, boundary.AddTicks(1));
        Assert.NotSame(at, after);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 17, 0, 0, TimeSpan.Zero), after.FollowedBy!.AtUtc);
        Assert.Same(Kzarka, Assert.Single(after.FollowedBy.Bosses));
    }

    [Fact]
    public void Gives_the_same_board_until_the_next_spawn_passes()
    {
        var data = Data("several");
        var cache = new BossBoardCache();

        var board = cache.Get(data, TuesdayNoonBerlin);
        Assert.Same(board, cache.Get(data, Utc(22, 13, 59)));

        var moved = cache.Get(data, Utc(22, 14, 0));
        Assert.NotSame(board, moved);
        Assert.Equal("Nouver", moved.Previous!.Bosses.Single().Name);
        Assert.Same(moved, cache.Get(data, Utc(22, 16, 59)));
    }

    [Fact]
    public void A_new_data_instance_is_read_at_once()
    {
        var data = new AppData { Timers = [Kzarka] };
        var cache = new BossBoardCache();
        Assert.False(cache.Get(data, TuesdayNoonBerlin).Next!.Skipped);

        var skipped = data with { Muted = [new MutedOccurrence(Kzarka.Id, Utc(22, 17, 0))] };

        Assert.True(cache.Get(skipped, TuesdayNoonBerlin).Next!.Skipped);
    }

    [Fact]
    public void A_clock_set_back_reads_the_board_again()
    {
        var data = Data("several");
        var cache = new BossBoardCache();
        cache.Get(data, TuesdayNoonBerlin.AddDays(3));

        AssertSameBoard(BossBoard.Build(data, TuesdayNoonBerlin), cache.Get(data, TuesdayNoonBerlin));
    }

    [Fact]
    public void Overlay_content_and_week_grid_read_the_same_next_spawn_from_it()
    {
        var data = new AppData { Timers = [Nouver, Kzarka] };
        var cache = new BossBoardCache();
        var berlin = TimeZones.Find("Europe/Berlin");

        var content = OverlayContent.Build(data, new OverlaySettings(), TuesdayNoonBerlin, cache);
        var grid = WeekGrid.Build(data, TuesdayNoonBerlin, berlin, cache);

        Assert.Same(cache.Get(data, TuesdayNoonBerlin).Next, content.Next);
        Assert.Equal(Utc(22, 14, 0), grid.Rows.SelectMany(r => r.Days).SelectMany(d => d)
            .Single(e => e.State == CellState.Next).AtUtc);
    }
}
