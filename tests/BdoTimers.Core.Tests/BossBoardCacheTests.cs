using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;
using static BdoTimers.Core.Tests.TestTimers;

namespace BdoTimers.Core.Tests;

public class BossBoardCacheTests
{
    static readonly TimerDef Nouver = Boss("Nouver", DayOfWeek.Tuesday, 16, 0);
    static readonly TimerDef Kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 19, 0);
    static readonly TimerDef Uturi = Boss("Uturi", DayOfWeek.Tuesday, 19, 0);
    static readonly TimerDef Garmoth = Boss("Garmoth", DayOfWeek.Thursday, 23, 15);

    static void AssertSameBoard(BossBoardState expected, BossBoardState actual)
    {
        static object? Shape(SpawnGroup? g) =>
            g is null ? null : (g.AtUtc, string.Join(",", g.Bosses.Select(b => b.Id)), g.Skipped);
        Assert.Equal(Shape(expected.Previous), Shape(actual.Previous));
        Assert.Equal(Shape(expected.Next), Shape(actual.Next));
        Assert.Equal(Shape(expected.FollowedBy), Shape(actual.FollowedBy));
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
    public void Matches_a_fresh_board_as_time_moves_on(string timetable)
    {
        var data = Data(timetable);
        var cache = new BossBoardCache();
        var times = Enumerable.Range(0, 9 * 24 * 60 / 37).Select(i => BerlinNoon.AddMinutes(37 * i))
            .Concat([Utc(22, 17, 0).AddSeconds(-1), Utc(22, 17, 0), Utc(22, 17, 0).AddSeconds(1)])
            .Concat([Utc(28, 17, 0).AddSeconds(-1), Utc(28, 17, 0), Utc(28, 17, 0).AddSeconds(1)])
            .Order();

        foreach (var now in times) AssertSameBoard(BossBoard.Build(data, now), cache.Get(data, now));
    }

    [Fact]
    public void Gives_the_same_board_until_the_next_spawn_passes()
    {
        var data = Data("several");
        var cache = new BossBoardCache();

        var board = cache.Get(data, BerlinNoon);
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
        Assert.False(cache.Get(data, BerlinNoon).Next!.Skipped);

        var skipped = data with { Muted = [new MutedOccurrence(Kzarka.Id, Utc(22, 17, 0))] };

        Assert.True(cache.Get(skipped, BerlinNoon).Next!.Skipped);
    }

    [Fact]
    public void A_clock_set_back_reads_the_board_again()
    {
        var data = Data("several");
        var cache = new BossBoardCache();
        cache.Get(data, BerlinNoon.AddDays(3));

        AssertSameBoard(BossBoard.Build(data, BerlinNoon), cache.Get(data, BerlinNoon));
    }

    [Fact]
    public void Overlay_content_and_week_grid_read_the_same_next_spawn_from_it()
    {
        var data = new AppData { Timers = [Nouver, Kzarka] };
        var cache = new BossBoardCache();
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        var content = OverlayContent.Build(data, new OverlaySettings(), BerlinNoon, cache);
        var grid = WeekGrid.Build(data, BerlinNoon, berlin, cache);

        Assert.Same(cache.Get(data, BerlinNoon).Next, content.Next);
        Assert.Equal(Utc(22, 14, 0), grid.Rows.SelectMany(r => r.Days).SelectMany(d => d)
            .Single(e => e.State == CellState.Next).AtUtc);
    }
}
