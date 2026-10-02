using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class BossSeedDataTests
{
    [Fact]
    public void Embedded_eu_seed_is_well_formed()
    {
        var seed = SeedService.LoadEmbedded();

        Assert.NotNull(TimeZones.Find(seed.TimeZoneId));
        Assert.True(seed.Bosses.Count >= 5, "EU has at least five scheduled world bosses");
        Assert.Contains(seed.Bosses, b => b.Name == "Kzarka");
        Assert.Contains(seed.Bosses, b => b.Name == "Nouver");
        Assert.All(seed.Bosses, b => Assert.NotEmpty(b.Slots));
        Assert.Equal(seed.Bosses.Count, seed.Bosses.Select(b => b.Name).Distinct().Count());

        var timers = SeedService.ToTimers(seed, new());
        var now = new FakeClock(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
        Assert.All(timers, t => Assert.NotEmpty(ScheduleMath.Next(t.Scheduled!, now.UtcNow, 1)));
    }
}
