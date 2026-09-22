namespace BdoTimers.Core.Model;

public sealed record AppSettings
{
    public bool Autostart { get; init; } = true;
    public float Volume { get; init; } = 0.8f;
    public string? TtsVoice { get; init; }
    public int TtsRate { get; init; }
    public IReadOnlyList<int> DefaultLeadTimesMinutes { get; init; } = [15, 5, 1, 0];
    public double? OverlayLeft { get; init; }
    public double? OverlayTop { get; init; }
    /// <summary>DateTimeOffset.MaxValue means paused until resumed.</summary>
    public DateTimeOffset? AlertsPausedUntilUtc { get; init; }
    public bool PriorityHintShown { get; init; }
}
