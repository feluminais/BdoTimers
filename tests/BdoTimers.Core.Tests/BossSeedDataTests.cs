using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class BossSeedDataTests
{
    [Fact]
    public void Na_seed_matches_verified_roster_counts_and_region_specific_weekend_slots()
    {
        var seed = SeedService.LoadEmbedded(BossRegions.NorthAmerica);
        Assert.Equal("America/Los_Angeles", seed.TimeZoneId);
        Assert.Equal("2026-10-02", seed.VerifiedOn);
        Assert.Contains("https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=83", seed.SourceUrls!);
        Assert.Equal(13, seed.Bosses.Count);
        Assert.Equal(84, seed.Bosses.Sum(b => b.Slots.Count));
        Assert.Equal(14, seed.Bosses.Single(b => b.Name == "Garmoth").Slots.Count);
        foreach (var name in new[] { "Quint", "Muraka" })
        {
            Assert.Equal([new BossSeedSlot(DayOfWeek.Thursday, "14:00"), new(DayOfWeek.Saturday, "17:00")],
                seed.Bosses.Single(b => b.Name == name).Slots);
        }
        Assert.Equal([new BossSeedSlot(DayOfWeek.Wednesday, "17:00"), new(DayOfWeek.Sunday, "14:00")],
            seed.Bosses.Single(b => b.Name == "Vell").Slots);
        Assert.All(SeedService.ToTimers(seed, new(), BossRegions.NorthAmerica), t =>
            Assert.Equal(BossRegions.NorthAmerica, t.BossRegionId));
    }

    [Theory]
    [InlineData("2026-03-07T08:00:00Z", "2026-03-07T20:00:00Z")]
    [InlineData("2026-03-08T09:59:00Z", "2026-03-08T19:00:00Z")]
    [InlineData("2026-10-25T00:00:00Z", "2026-10-25T19:00:00Z")]
    [InlineData("2026-11-01T08:59:00Z", "2026-11-01T20:00:00Z")]
    public void Na_regular_noon_slot_uses_pacific_dst_not_european_dates(string from, string expected)
    {
        var garmoth = SeedService.ToTimers(SeedService.LoadEmbedded("na"), new(), "na").Single(t => t.Name == "Garmoth");
        Assert.Equal(DateTimeOffset.Parse(expected), ScheduleMath.Next(garmoth.Scheduled!, DateTimeOffset.Parse(from), 1)[0]);
    }

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
        var now = new FakeClock(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.All(timers, t => Assert.NotEmpty(ScheduleMath.Next(t.Scheduled!, now.UtcNow, 1)));
    }
}
