using BdoTimers.Core.Seed;
using System.Text.Json.Serialization;

namespace BdoTimers.Core.Model;

public readonly record struct MutedOccurrence(Guid TimerId, DateTimeOffset OccurrenceUtc);

[Storage.SavedVersion(nameof(AppData.DataVersion), Storage.DataMigrations.Current)]
public sealed record AppData
{
    public IReadOnlyList<TimerDef> Timers { get; init; } = [];
    public IReadOnlyList<MutedOccurrence> Muted { get; init; } = [];
    /// <summary>Runtime completion snapshots keep queued end alerts eligible after countdowns reset or disappear.</summary>
    [JsonIgnore]
    public IReadOnlyList<TimerDef> CompletedCountdowns { get; init; } = [];
    public string SelectedBossRegion { get; init; } = Seed.BossRegions.Europe;
    public IReadOnlyList<BossRegionState> BossRegions { get; init; } = [];
    /// <summary>Identifies a selection so queued alerts cannot survive switching away and back.</summary>
    public Guid BossSelectionVersion { get; init; }
    /// <summary>Boss leads already due when a region was selected are suppressed, including after a clock rollback.</summary>
    public DateTimeOffset? BossAlertsAfterUtc { get; init; }
    /// <summary>Legacy EU seed flag, consumed by migration.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SeedApplied { get; init; }
    /// <summary>Legacy EU baseline, consumed by migration.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BossSeed? AcceptedBossTimetable { get; init; }
    /// <summary>Which <see cref="Storage.DataMigrations"/> have run; files from before migrations read as 0.</summary>
    public int DataVersion { get; init; }
}
