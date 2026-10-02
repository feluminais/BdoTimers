using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class PresetsTests
{
    [Fact]
    public void Ensure_puts_all_presets_ahead_of_existing_timers()
    {
        var own = new TimerDef { Name = "Buff", Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };
        var data = Presets.Ensure(new AppData { Timers = [own] });

        Assert.Equal(["Farm", "Fishing", "Guild bosses", "Guild war", "Buff"], data.Timers.Select(t => t.Name));
        var farm = data.Timers[0];
        Assert.Equal(TimerKind.Countdown, farm.Kind);
        Assert.Equal(TimeSpan.FromHours(22), farm.Countdown!.Duration);
        Assert.Equal(TimerKind.Stopwatch, data.Timers[1].Kind);
        Assert.NotNull(data.Timers[1].Stopwatch);
        Assert.All(data.Timers.Skip(2).Take(2), t =>
        {
            Assert.Equal(TimerKind.Scheduled, t.Kind);
            Assert.Equal(TimeZoneInfo.Local.Id, t.Scheduled!.TimeZoneId);
            Assert.Empty(t.Scheduled.Slots);
        });
    }

    [Fact]
    public void Ensure_keeps_presets_the_user_has_changed()
    {
        var once = Presets.Ensure(new AppData());
        var renamed = once with { Timers = once.Timers.Select(t => t with { Name = t.Name + "!" }).ToList() };

        Assert.Same(renamed, Presets.Ensure(renamed));
    }

    [Fact]
    public void Ensure_adds_guild_presets_to_existing_data_without_resetting_farm_or_fishing()
    {
        var old = Presets.Create().Take(2).Select(t => t with { Name = t.Name + "!" }).ToList();
        var own = new TimerDef { Name = "Buff", Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };

        var upgraded = Presets.Ensure(new AppData { Timers = [own, .. old] });

        Assert.Equal([Presets.Farm, Presets.Fishing, Presets.GuildBosses, Presets.GuildWar, null],
            upgraded.Timers.Select(t => t.Preset));
        Assert.Same(old[0], upgraded.Timers[0]);
        Assert.Same(old[1], upgraded.Timers[1]);
        Assert.Same(own, upgraded.Timers[4]);
        Assert.Same(upgraded, Presets.Ensure(upgraded));
    }

    [Fact]
    public void Rank_orders_presets_first()
    {
        Assert.True(Presets.Rank(Presets.Farm) < Presets.Rank(Presets.Fishing));
        Assert.True(Presets.Rank(Presets.Fishing) < Presets.Rank(Presets.HorseRegistration));
        Assert.True(Presets.Rank(Presets.HorseRegistration) < Presets.Rank(Presets.GuildBosses));
        Assert.True(Presets.Rank(Presets.GuildBosses) < Presets.Rank(Presets.GuildWar));
        Assert.True(Presets.Rank(Presets.GuildWar) < Presets.Rank(null));
    }

    [Fact]
    public void Horse_registration_is_a_ten_minute_countdown_that_ensure_does_not_add_back()
    {
        var horse = Presets.CreateHorseRegistration();

        Assert.Equal("Horse registration", horse.Name);
        Assert.Equal(Presets.HorseRegistration, horse.Preset);
        Assert.Equal(TimerKind.Countdown, horse.Kind);
        Assert.Equal(TimeSpan.FromMinutes(10), horse.Countdown!.Duration);
        Assert.Equal(DefaultHotkeys.HorseRegistration, horse.StartHotkey);
        Assert.DoesNotContain(Presets.Ensure(new AppData()).Timers, t => t.Preset == Presets.HorseRegistration);
    }

    [Fact]
    public void Horse_registration_alerts_a_minute_before_and_at_the_end_whatever_the_default_times()
    {
        var horse = Presets.CreateHorseRegistration();
        horse = horse with { Countdown = CountdownOps.Start(horse.Countdown!, T0) };
        var planner = new AlertPlanner();
        var none = new HashSet<MutedOccurrence>();
        var defaults = AlertConfig.StandardLeadTimesMinutes;

        Assert.Empty(planner.Tick([horse], none, T0, defaults));
        Assert.Equal(1, Assert.Single(planner.Tick([horse], none, T0.AddMinutes(9), defaults)).LeadMinutes);
        Assert.Equal(0, Assert.Single(planner.Tick([horse], none, T0.AddMinutes(10), defaults)).LeadMinutes);
    }

    [Fact]
    public void Of_the_presets_only_horse_registration_can_be_deleted()
    {
        Assert.False(Presets.CanDelete(Presets.Farm));
        Assert.False(Presets.CanDelete(Presets.Fishing));
        Assert.False(Presets.CanDelete(Presets.GuildBosses));
        Assert.False(Presets.CanDelete(Presets.GuildWar));
        Assert.True(Presets.CanDelete(Presets.HorseRegistration));
        Assert.True(Presets.CanDelete(null));
    }

    [Fact]
    public void Guild_presets_start_without_occurrences_and_have_fixed_slot_limits()
    {
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var bosses = Presets.Create().Single(t => t.Preset == Presets.GuildBosses);
        var war = Presets.Create().Single(t => t.Preset == Presets.GuildWar);

        Assert.Empty(OccurrenceSource.Between(bosses, now, now.AddDays(8)));
        Assert.Empty(OccurrenceSource.Between(war, now, now.AddDays(8)));
        Assert.Equal(0, Presets.MinimumSlots(bosses.Preset));
        Assert.Equal(1, Presets.MaximumSlots(bosses.Preset));
        Assert.Equal(0, Presets.MinimumSlots(war.Preset));
        Assert.Null(Presets.MaximumSlots(war.Preset));
        Assert.Equal(1, Presets.MinimumSlots(null));
    }

    [Fact]
    public void Guild_bosses_repeats_weekly_and_guild_war_accepts_several_times()
    {
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var boss = Presets.Create().Single(t => t.Preset == Presets.GuildBosses);
        var war = Presets.Create().Single(t => t.Preset == Presets.GuildWar);
        var tuesday = new Slot(DayOfWeek.Tuesday, new TimeOnly(20, 0));
        var friday = new Slot(DayOfWeek.Friday, new TimeOnly(20, 0));
        boss = boss with { Scheduled = boss.Scheduled! with { Slots = [tuesday] } };
        war = war with { Scheduled = war.Scheduled! with { Slots = [tuesday, friday] } };

        Assert.Equal(2, OccurrenceSource.Between(boss, now, now.AddDays(9)).Count());
        Assert.Equal(3, OccurrenceSource.Between(war, now, now.AddDays(9)).Count());
    }

    [Fact]
    public void Guild_war_alerts_at_each_configured_weekly_time()
    {
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var war = Presets.Create().Single(t => t.Preset == Presets.GuildWar);
        war = war with
        {
            Scheduled = new ScheduledSpec
            {
                TimeZoneId = "Europe/Berlin",
                Slots = [new Slot(DayOfWeek.Tuesday, new TimeOnly(20, 0)),
                         new Slot(DayOfWeek.Friday, new TimeOnly(20, 0))],
            },
            Alerts = new AlertConfig { LeadTimesMinutes = [0] },
        };
        var events = OccurrenceSource.Between(war, now, now.AddDays(8)).Take(2).ToList();
        var planner = new AlertPlanner();
        HashSet<MutedOccurrence> none = [];

        Assert.Equal(2, events.Count);
        Assert.Equal(events[0], Assert.Single(planner.Tick([war], none, events[0])).OccurrenceUtc);
        Assert.Empty(planner.Tick([war], none, events[0].AddMinutes(1)));
        Assert.Equal(events[1], Assert.Single(planner.Tick([war], none, events[1])).OccurrenceUtc);
    }

    [Fact]
    public void A_stopwatch_never_alerts()
    {
        var fishing = Presets.Create()[1] with { Stopwatch = StopwatchOps.Start(new StopwatchSpec(), T0) };

        Assert.Empty(OccurrenceSource.Between(fishing, T0, T0.AddDays(8)));
    }
}
