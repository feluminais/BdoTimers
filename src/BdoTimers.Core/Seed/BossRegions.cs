using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <param name="TimeZoneId">The region's server time, which its embedded timetable is written in.</param>
public sealed record BossRegion(string Id, string Label, string ShortLabel, string ResourceName, string TimeZoneId)
{
    /// <summary>War of the Roses in server time: when Third Legion applications close and when the battle starts.</summary>
    public IReadOnlyList<Slot> WarOfTheRoses { get; init; } = [];

    public DateTimeOffset ServerTime(DateTimeOffset now) =>
        TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
}

/// <summary>
/// Adding a verified region only requires its catalog entry and embedded timetable. Everything else that differs between
/// regions is in the entry; docs/boss-region-sources.md lists it with its sources.
/// </summary>
public static class BossRegions
{
    public const string Europe = "eu";
    public const string NorthAmerica = "na";
    public static IReadOnlyList<BossRegion> All { get; } =
    [
        new(Europe, "Europe", "EU", "BdoTimers.Core.Data.bosses.eu.json", "Europe/Berlin")
        {
            WarOfTheRoses = [new(DayOfWeek.Sunday, new(15, 5), "Applications close"), new(DayOfWeek.Sunday, new(17, 0), "Battle")],
        },
        new(NorthAmerica, "North America", "NA", "BdoTimers.Core.Data.bosses.na.json", "America/Los_Angeles")
        {
            WarOfTheRoses = [new(DayOfWeek.Sunday, new(13, 5), "Applications close"), new(DayOfWeek.Sunday, new(15, 0), "Battle")],
        },
    ];

    public static BossRegion Find(string id) => All.FirstOrDefault(r => r.Id == id)
        ?? throw new ArgumentException($"Unknown boss region '{id}'.", nameof(id));

    public static string RegionOf(TimerDef boss) => boss.BossRegionId ?? Europe;
    public static bool IsSelected(AppData data, TimerDef timer) =>
        timer.IsBuiltIn && RegionOf(timer) == data.SelectedBossRegion;
    public static bool IsEligible(AppData data, TimerDef timer) => !timer.IsBuiltIn || IsSelected(data, timer);

    public static BossRegionState State(AppData data, string? regionId = null)
    {
        var id = regionId ?? data.SelectedBossRegion;
        return data.BossRegions.FirstOrDefault(r => r.RegionId == id) ?? new BossRegionState
        {
            RegionId = id,
            SeedApplied = id == Europe && data.SeedApplied,
            AcceptedBossTimetable = id == Europe ? data.AcceptedBossTimetable : null,
        };
    }

    public static AppData WithState(AppData data, BossRegionState state)
    {
        var regions = data.BossRegions;
        if (state.RegionId != Europe && !regions.Any(r => r.RegionId == Europe)
            && (data.SeedApplied || data.AcceptedBossTimetable is not null))
            regions = [.. regions, State(data, Europe)];
        return data with
        {
            BossRegions = regions.Any(r => r.RegionId == state.RegionId)
                ? regions.Select(r => r.RegionId == state.RegionId ? state : r).ToList()
                : [.. regions, state],
            SeedApplied = false, AcceptedBossTimetable = null,
        };
    }

    /// <summary>Assigns the existing configuration to EU without replacing any boss or schedule.</summary>
    public static AppData MigrateLegacy(AppData data, AppSettings settings)
    {
        var eu = State(data, Europe);
        eu = eu with
        {
            SeedApplied = eu.SeedApplied || data.Timers.Any(t => t.IsBuiltIn),
            TimetableNoticeRevision = eu.TimetableNoticeRevision ?? settings.TimetableNoticeRevision,
        };
        return WithState(data with
        {
            SelectedBossRegion = Europe,
            Timers = data.Timers.Select(t => t.IsBuiltIn && t.BossRegionId is null
                ? t with { BossRegionId = Europe } : t).ToList(),
        }, eu);
    }
}
