using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class SeedServiceTests
{
    static readonly BossSeed Seed = new("Europe/Berlin",
    [
        new BossSeedEntry("Kzarka", [new BossSeedSlot(DayOfWeek.Monday, "00:15"), new BossSeedSlot(DayOfWeek.Friday, "19:00")]),
        new BossSeedEntry("Nouver", [new BossSeedSlot(DayOfWeek.Tuesday, "14:00")]),
    ]);

    static readonly AlertConfig Defaults = new() { LeadTimesMinutes = [10, 0] };

    [Fact]
    public void Converts_seed_to_builtin_scheduled_timers()
    {
        var timers = SeedService.ToTimers(Seed, Defaults);

        Assert.Equal(new[] { "Kzarka", "Nouver" }, timers.Select(t => t.Name));
        Assert.All(timers, t =>
        {
            Assert.True(t.IsBuiltIn);
            Assert.Equal(TimerKind.Scheduled, t.Kind);
            Assert.Equal("Europe/Berlin", t.Scheduled!.TimeZoneId);
            Assert.Equal(Defaults, t.Alerts);
        });
        Assert.Equal(new Slot(DayOfWeek.Friday, new TimeOnly(19, 0)), timers[0].Scheduled!.Slots[1]);
    }

    [Fact]
    public void Applies_once_only()
    {
        var once = SeedService.ApplyIfNeeded(new AppData(), Seed, Defaults);
        var twice = SeedService.ApplyIfNeeded(once, Seed, Defaults);

        Assert.True(once.SeedApplied);
        Assert.Equal(2, once.Timers.Count);
        Assert.Same(once, twice);
    }

    [Fact]
    public void Reset_replaces_builtins_keeps_custom_timers_and_alert_settings()
    {
        var custom = new TimerDef { Name = "Farm", Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };
        var applied = SeedService.ApplyIfNeeded(new AppData { Timers = [custom] }, Seed, Defaults);
        var tunedAlerts = new AlertConfig { LeadTimesMinutes = [30] };
        var edited = applied with
        {
            Timers = applied.Timers
                .Where(t => t.Name != "Nouver")
                .Select(t => t.Name == "Kzarka" ? t with { Alerts = tunedAlerts, Scheduled = new ScheduledSpec() } : t)
                .ToList(),
        };

        var reset = SeedService.ResetBuiltIns(edited, Seed, Defaults);

        Assert.Contains(reset.Timers, t => t.Id == custom.Id);
        var kzarka = reset.Timers.Single(t => t.Name == "Kzarka");
        Assert.Equal(2, kzarka.Scheduled!.Slots.Count);
        Assert.Equal(tunedAlerts, kzarka.Alerts);
        Assert.Contains(reset.Timers, t => t.Name == "Nouver");
        Assert.Equal(3, reset.Timers.Count);
    }
}
