using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;
using static BdoTimers.Core.Tests.TestTimers;

namespace BdoTimers.Core.Tests;

public class BossBoardTests
{
    [Fact]
    public void Simultaneous_spawns_share_a_group()
    {
        var data = new AppData
        {
            Timers =
            [
                Boss("Uturi", DayOfWeek.Tuesday, 19, 0),
                Boss("Nouver", DayOfWeek.Tuesday, 16, 0),
                Boss("Kzarka", DayOfWeek.Tuesday, 19, 0),
            ],
        };

        var board = BossBoard.Build(data, BerlinNoon);

        Assert.Equal(Utc(22, 14, 0), board.Next!.AtUtc);
        Assert.Equal(new[] { "Nouver" }, board.Next.Bosses.Select(b => b.Name));
        Assert.Equal(Utc(22, 17, 0), board.FollowedBy!.AtUtc);
        Assert.Equal(new[] { "Uturi", "Kzarka" }, board.FollowedBy.Bosses.Select(b => b.Name));
    }

    [Fact]
    public void Unfollowed_bosses_and_custom_timers_are_ignored()
    {
        var data = new AppData
        {
            Timers =
            [
                Boss("Nouver", DayOfWeek.Tuesday, 16, 0) with { Enabled = false },
                TestTimers.Scheduled("Guild meeting", DayOfWeek.Tuesday, 13, 0, 0),
                Boss("Kzarka", DayOfWeek.Tuesday, 19, 0),
            ],
        };

        var board = BossBoard.Build(data, BerlinNoon);

        Assert.Equal("Kzarka", board.Next!.Bosses.Single().Name);
        Assert.Equal("Kzarka", board.Previous!.Bosses.Single().Name);
        Assert.Equal(Utc(15, 17, 0), board.Previous.AtUtc);
    }

    [Fact]
    public void Previous_is_found_in_the_prior_week()
    {
        var data = new AppData
        {
            Timers = [Boss("Garmoth", DayOfWeek.Sunday, 23, 15), Boss("Karanda", DayOfWeek.Monday, 14, 0)],
        };
        var mondayJustAfterMidnight = Utc(27, 22, 5);

        var board = BossBoard.Build(data, mondayJustAfterMidnight);

        Assert.Equal(Utc(27, 21, 15), board.Previous!.AtUtc);
        Assert.Equal(Utc(28, 12, 0), board.Next!.AtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 21, 15, 0, TimeSpan.Zero), board.FollowedBy!.AtUtc);
    }

    [Fact]
    public void Group_is_skipped_only_when_every_boss_is_muted()
    {
        var kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 19, 0);
        var uturi = Boss("Uturi", DayOfWeek.Tuesday, 19, 0);
        var at = Utc(22, 17, 0);

        var one = BossBoard.Build(new AppData { Timers = [kzarka, uturi], Muted = [new(kzarka.Id, at)] }, BerlinNoon);
        var both = BossBoard.Build(
            new AppData { Timers = [kzarka, uturi], Muted = [new(kzarka.Id, at), new(uturi.Id, at)] }, BerlinNoon);

        Assert.False(one.Next!.Skipped);
        Assert.True(both.Next!.Skipped);
    }

    [Fact]
    public void A_spawn_at_the_current_instant_counts_as_previous()
    {
        var data = new AppData
        {
            Timers = [Boss("Nouver", DayOfWeek.Tuesday, 16, 0), Boss("Kzarka", DayOfWeek.Tuesday, 19, 0)],
        };

        var board = BossBoard.Build(data, Utc(22, 14, 0));

        Assert.Equal("Nouver", board.Previous!.Bosses.Single().Name);
        Assert.Equal("Kzarka", board.Next!.Bosses.Single().Name);
    }

    [Fact]
    public void No_followed_bosses_gives_an_empty_board()
    {
        var board = BossBoard.Build(new AppData(), BerlinNoon);
        Assert.Null(board.Previous);
        Assert.Null(board.Next);
        Assert.Null(board.FollowedBy);
    }
}
