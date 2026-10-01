using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class PresetsTests
{
    [Fact]
    public void Ensure_puts_farm_and_fishing_ahead_of_existing_timers()
    {
        var own = new TimerDef { Name = "Buff", Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };
        var data = Presets.Ensure(new AppData { Timers = [own] });

        Assert.Equal(["Farm", "Fishing", "Buff"], data.Timers.Select(t => t.Name));
        var farm = data.Timers[0];
        Assert.Equal(TimerKind.Countdown, farm.Kind);
        Assert.Equal(TimeSpan.FromHours(22), farm.Countdown!.Duration);
        Assert.Equal(TimerKind.Stopwatch, data.Timers[1].Kind);
        Assert.NotNull(data.Timers[1].Stopwatch);
    }

    [Fact]
    public void Ensure_keeps_presets_the_user_has_changed()
    {
        var once = Presets.Ensure(new AppData());
        var renamed = once with { Timers = once.Timers.Select(t => t with { Name = t.Name + "!" }).ToList() };

        Assert.Same(renamed, Presets.Ensure(renamed));
    }

    [Fact]
    public void Rank_orders_presets_first()
    {
        Assert.True(Presets.Rank(Presets.Farm) < Presets.Rank(Presets.Fishing));
        Assert.True(Presets.Rank(Presets.Fishing) < Presets.Rank(Presets.HorseRegistration));
        Assert.True(Presets.Rank(Presets.HorseRegistration) < Presets.Rank(null));
    }

    [Fact]
    public void Horse_registration_is_a_ten_minute_countdown_that_ensure_does_not_add_back()
    {
        var horse = Presets.CreateHorseRegistration();

        Assert.Equal("Horse registration", horse.Name);
        Assert.Equal(Presets.HorseRegistration, horse.Preset);
        Assert.Equal(TimerKind.Countdown, horse.Kind);
        Assert.Equal(TimeSpan.FromMinutes(10), horse.Countdown!.Duration);
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
        Assert.True(Presets.CanDelete(Presets.HorseRegistration));
        Assert.True(Presets.CanDelete(null));
    }

    [Fact]
    public void A_stopwatch_never_alerts()
    {
        var fishing = Presets.Create()[1] with { Stopwatch = StopwatchOps.Start(new StopwatchSpec(), T0) };

        Assert.Empty(OccurrenceSource.Between(fishing, T0, T0.AddDays(8)));
    }
}
