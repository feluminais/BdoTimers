namespace BdoTimers.Core.Scheduling;

/// <summary>How close the next spawn is; the hero's clock and the top-bar chip change colour with it.</summary>
public enum UrgencyLevel { Normal, Soon, Imminent, Now }

public static class Urgency
{
    /// <summary>Soon within 30 minutes, Imminent within 10, Now within 1.</summary>
    public static UrgencyLevel Of(TimeSpan untilSpawn) =>
        untilSpawn <= TimeSpan.FromMinutes(1) ? UrgencyLevel.Now
        : untilSpawn <= TimeSpan.FromMinutes(10) ? UrgencyLevel.Imminent
        : untilSpawn <= TimeSpan.FromMinutes(30) ? UrgencyLevel.Soon
        : UrgencyLevel.Normal;
}
