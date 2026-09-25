using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

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
        Assert.True(Presets.Rank(Presets.Fishing) < Presets.Rank(null));
    }

    [Fact]
    public void A_stopwatch_never_alerts()
    {
        var t0 = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var fishing = Presets.Create()[1] with { Stopwatch = StopwatchOps.Start(new StopwatchSpec(), t0) };

        Assert.Empty(OccurrenceSource.Between(fishing, t0, t0.AddDays(8)));
    }
}
