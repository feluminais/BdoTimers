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

    [Fact]
    public void Reset_keeps_each_boss_id_and_alerts_off_and_drops_mutes_of_removed_bosses()
    {
        var applied = SeedService.ApplyIfNeeded(new AppData(), Seed, Defaults);
        var kzarka = applied.Timers.Single(t => t.Name == "Kzarka");
        var retired = new TimerDef { Name = "Retired", Kind = TimerKind.Scheduled, IsBuiltIn = true, Scheduled = new ScheduledSpec() };
        var at = new DateTimeOffset(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);
        var data = applied with
        {
            Timers = [.. applied.Timers.Select(t => t.Id == kzarka.Id ? t with { Name = "KZARKA", Enabled = false } : t), retired],
            Muted = [new MutedOccurrence(kzarka.Id, at), new MutedOccurrence(retired.Id, at)],
        };

        var reset = SeedService.ResetBuiltIns(data, Seed, Defaults);

        var kept = reset.Timers.Single(t => t.Name == "Kzarka");
        Assert.Equal(kzarka.Id, kept.Id);
        Assert.False(kept.Enabled);
        Assert.Equal(applied.Timers.Single(t => t.Name == "Nouver").Id, reset.Timers.Single(t => t.Name == "Nouver").Id);
        Assert.DoesNotContain(reset.Timers, t => t.Id == retired.Id);
        Assert.Equal([new MutedOccurrence(kzarka.Id, at)], reset.Muted);
    }
}
