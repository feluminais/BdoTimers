namespace BdoTimers.Core.Model;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height);

public sealed record AppSettings
{
    /// <summary>Off until the user turns it on in Settings.</summary>
    public bool Autostart { get; init; }
    public float Volume { get; init; } = 0.8f;
    /// <summary>Sound key played for timers whose sound is "Default"; see <see cref="Sounds.SoundKeys"/>.</summary>
    public string AlertSound { get; init; } = BuiltInSounds.Default;
    public string? TtsVoice { get; init; }
    public int TtsRate { get; init; }
    public IReadOnlyList<int> DefaultLeadTimesMinutes { get; init; } = AlertConfig.StandardLeadTimesMinutes;
    public double? OverlayLeft { get; init; }
    public double? OverlayTop { get; init; }
    /// <summary>DateTimeOffset.MaxValue means paused until resumed.</summary>
    public DateTimeOffset? AlertsPausedUntilUtc { get; init; }
    public bool PriorityHintShown { get; init; }
    public WindowPlacement? Window { get; init; }
}
