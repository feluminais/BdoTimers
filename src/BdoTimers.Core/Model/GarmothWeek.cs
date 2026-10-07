namespace BdoTimers.Core.Model;

/// <summary>
/// Garmoth's kills this week while the tracker is on: he can be done three times a week, and once the third is marked his
/// spawns are out until the weekly reset clears the count.
/// </summary>
public sealed record GarmothWeek
{
    /// <summary>0 to <see cref="Scheduling.GarmothTracker.Limit"/>.</summary>
    public int Kills { get; init; }
    /// <summary>When the last of the weekly kills was marked; null until then.</summary>
    public DateTimeOffset? DoneAtUtc { get; init; }
    /// <summary>When the count clears: the weekly reset in Settings, as of the last time it was looked at.</summary>
    public DateTimeOffset ResetUtc { get; init; }
}
