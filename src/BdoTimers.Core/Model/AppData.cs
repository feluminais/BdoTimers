namespace BdoTimers.Core.Model;

public readonly record struct MutedOccurrence(Guid TimerId, DateTimeOffset OccurrenceUtc);

public sealed record AppData
{
    public IReadOnlyList<TimerDef> Timers { get; init; } = [];
    public IReadOnlyList<MutedOccurrence> Muted { get; init; } = [];
    /// <summary>True once the embedded boss seed has been copied into Timers.</summary>
    public bool SeedApplied { get; init; }
}
