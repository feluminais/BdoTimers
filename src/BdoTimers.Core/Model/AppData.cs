using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Model;

public readonly record struct MutedOccurrence(Guid TimerId, DateTimeOffset OccurrenceUtc);

public sealed record AppData
{
    public IReadOnlyList<TimerDef> Timers { get; init; } = [];
    public IReadOnlyList<MutedOccurrence> Muted { get; init; } = [];
    /// <summary>True once the embedded boss seed has been copied into Timers.</summary>
    public bool SeedApplied { get; init; }
    /// <summary>The last bundled timetable reviewed by the player; saved separately from their edited spawn times.</summary>
    public BossSeed? AcceptedBossTimetable { get; init; }
    /// <summary>Which <see cref="Storage.DataMigrations"/> have run; files from before migrations read as 0.</summary>
    public int DataVersion { get; init; }
}
