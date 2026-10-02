using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Model;

/// <summary>A region's accepted bundle is independent of the player's edited boss schedules.</summary>
public sealed record BossRegionState
{
    public string RegionId { get; init; } = BossRegions.Europe;
    public bool SeedApplied { get; init; }
    public BossSeed? AcceptedBossTimetable { get; init; }
    public string? TimetableNoticeRevision { get; init; }
}
