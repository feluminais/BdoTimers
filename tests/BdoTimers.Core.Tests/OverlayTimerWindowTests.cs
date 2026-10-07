using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class OverlayTimerWindowTests
{
    static readonly OverlaySettings Quiet = new()
    {
        ShowClock = false, ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false, ShowCustomTimers = false,
    };

    /// <summary>Tuesday 12:20 in Berlin, 20 minutes after <see cref="BerlinNoon"/>.</summary>
    static readonly TimerDef GuildWar = TestTimers.Scheduled("Guild war", DayOfWeek.Tuesday, 12, 20);
    static readonly DateTimeOffset Spawn = BerlinNoon.AddMinutes(20);

    static OverlaySettings Listed(TimerDef timer, int minutes) =>
        Quiet with { Timers = [new OverlayTimerWindow(timer.Id, minutes)] };

    [Fact]
    public void A_listed_timer_shows_from_its_window_until_it_happens()
    {
        var data = new AppData { Timers = [GuildWar] };
        var settings = Listed(GuildWar, 180);

        Assert.Empty(UpcomingQuery.Listed(data, settings, Spawn.AddMinutes(-181)));
        var item = Assert.Single(UpcomingQuery.Listed(data, settings, Spawn.AddMinutes(-180)));
        Assert.Equal((GuildWar.Id, Spawn), (item.Timer.Id, item.AtUtc));
        Assert.Single(UpcomingQuery.Listed(data, settings, Spawn));
        Assert.Empty(UpcomingQuery.Listed(data, settings, Spawn.AddTicks(1)));
    }

    [Fact]
    public void Unlisted_timers_and_a_missing_timer_get_no_row()
    {
        var data = new AppData { Timers = [GuildWar] };
        var at = Spawn.AddMinutes(-10);

        Assert.Empty(UpcomingQuery.Listed(data, Quiet, at));
        Assert.Empty(UpcomingQuery.Listed(data, Listed(GuildWar, 0), at));
        Assert.Empty(UpcomingQuery.Listed(data, Quiet with { Timers = [new OverlayTimerWindow(Guid.NewGuid(), 180)] }, at));
    }

    [Fact]
    public void A_row_ignores_alerts_but_not_a_skipped_time()
    {
        var at = Spawn.AddMinutes(-60);
        var settings = Listed(GuildWar, 180);

        Assert.Single(UpcomingQuery.Listed(new AppData { Timers = [GuildWar with { Enabled = false }] }, settings, at));
        var skipped = new AppData { Timers = [GuildWar], Muted = [new MutedOccurrence(GuildWar.Id, Spawn)] };
        Assert.Empty(UpcomingQuery.Listed(skipped, settings, at));
    }

    [Fact]
    public void A_row_shows_with_the_overlay_but_never_brings_it_up()
    {
        var data = new AppData { Timers = [GuildWar] };
        var at = Spawn.AddMinutes(-60);
        var settings = Listed(GuildWar, 180);

        var content = OverlayContent.Build(data, settings, at);

        Assert.Equal(GuildWar.Id, Assert.Single(content.Upcoming).Timer.Id);
        Assert.False(content.IsEmpty);
        Assert.False(content.HasDuePopUp);
        Assert.False(new OverlayPresence().IsVisible(settings, at, content, previewing: false));
        Assert.True(new OverlayPresence().IsVisible(settings with { AlwaysShow = true }, at, content, previewing: false));
        Assert.True(OverlayContent.Build(data, settings, Spawn.AddMinutes(-181)).IsEmpty);
    }

    [Fact]
    public void An_occurrence_with_a_pop_up_row_gets_no_second_row()
    {
        var popUp = GuildWar with { Alerts = new AlertConfig { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 15 } } };
        var data = new AppData { Timers = [popUp] };
        var settings = Listed(popUp, 180);

        var near = OverlayContent.Build(data, settings, Spawn.AddMinutes(-10));
        Assert.Single(near.PopUps);
        Assert.Empty(near.Upcoming);

        var far = OverlayContent.Build(data, settings, Spawn.AddMinutes(-60));
        Assert.Empty(far.PopUps);
        Assert.Single(far.Upcoming);
    }
}
