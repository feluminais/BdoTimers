namespace BdoTimers.Core.Model;

public readonly record struct MutedOccurrence(Guid TimerId, DateTimeOffset OccurrenceUtc);

public sealed record AppData
{
    public IReadOnlyList<TimerDef> Timers { get; init; } = [];
    public IReadOnlyList<MutedOccurrence> Muted { get; init; } = [];
    /// <summary>Which <see cref="Storage.DataMigrations"/> have run; files from before migrations read as 0.</summary>
    public int DataVersion { get; init; }
}
