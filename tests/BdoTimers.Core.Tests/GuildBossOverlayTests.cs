using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class GuildBossOverlayTests
{
    static readonly OverlaySettings Off = new()
    {
        ShowClock = false, ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false, ShowCustomTimers = false,
    };
    static readonly OverlaySettings On = Off with { GuildBosses = new OverlayAlert { Enabled = true, ShowMinutesBefore = 15 } };

    /// <summary>Tuesday 12:20 in Berlin, 20 minutes after <see cref="BerlinNoon"/>.</summary>
    static readonly TimerDef GuildBoss = TestTimers.Scheduled("Guild bosses", DayOfWeek.Tuesday, 12, 20) with { Preset = Presets.GuildBosses };
    static readonly DateTimeOffset Spawn = BerlinNoon.AddMinutes(20);

    [Fact]
    public void Guild_bosses_pop_up_by_the_shared_setting_instead_of_their_own()
    {
        var own = GuildBoss with { Alerts = new AlertConfig { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 30 } } };
        var weekly = TestTimers.Scheduled("Weekly", DayOfWeek.Tuesday, 12, 20);
        var data = new AppData { Timers = [own, weekly] };

        Assert.Empty(UpcomingQuery.ForOverlay(data, Off, BerlinNoon));
        Assert.Null(UpcomingQuery.OverlayStart(data, Off, BerlinNoon));

        Assert.Empty(UpcomingQuery.ForOverlay(data, On, BerlinNoon));
        Assert.Equal(Spawn.AddMinutes(-15), UpcomingQuery.OverlayStart(data, On, BerlinNoon));
        var due = Assert.Single(UpcomingQuery.ForOverlay(data, On, Spawn.AddMinutes(-15)));
        Assert.Equal((own.Id, Spawn), (due.Timer.Id, due.AtUtc));
        Assert.Empty(UpcomingQuery.ForOverlay(data, On, Spawn.AddTicks(1)));
    }

    [Fact]
    public void A_due_guild_boss_brings_the_overlay_up_unless_its_alerts_are_off_or_the_spawn_is_skipped()
    {
        var at = Spawn.AddMinutes(-10);

        var content = OverlayContent.Build(new AppData { Timers = [GuildBoss] }, On, at);

        Assert.Equal(GuildBoss.Id, Assert.Single(content.PopUps).Timer.Id);
        Assert.True(new OverlayPresence().IsVisible(On, at, content, previewing: false));
        Assert.True(OverlayContent.Build(new AppData { Timers = [GuildBoss with { Enabled = false }] }, On, at).IsEmpty);
        var skipped = new AppData { Timers = [GuildBoss], Muted = [new MutedOccurrence(GuildBoss.Id, Spawn)] };
        Assert.True(OverlayContent.Build(skipped, On, at).IsEmpty);
    }

    [Fact]
    public void Gate_reads_changed_settings_at_once()
    {
        var data = new AppData { Timers = [GuildBoss] };
        var gate = new OverlayPopUpGate();
        var at = Spawn.AddMinutes(-10);

        Assert.False(gate.MayBeDue(data, Off, at));
        Assert.True(gate.MayBeDue(data, On, at));
        Assert.False(gate.MayBeDue(data, On with { GuildBosses = On.GuildBosses with { ShowMinutesBefore = 5 } }, at));
    }
}
