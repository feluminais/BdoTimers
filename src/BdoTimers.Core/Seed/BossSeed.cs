namespace BdoTimers.Core.Seed;

public sealed record BossSeed(
    string TimeZoneId,
    IReadOnlyList<BossSeedEntry> Bosses,
    string? Source = null,
    string? VerifiedOn = null,
    IReadOnlyList<string>? SourceUrls = null);

public sealed record BossSeedEntry(string Name, IReadOnlyList<BossSeedSlot> Slots);

/// <summary>Time is "HH:mm" in the seed's time zone.</summary>
public sealed record BossSeedSlot(DayOfWeek Day, string Time);
