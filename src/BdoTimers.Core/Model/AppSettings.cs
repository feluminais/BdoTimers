namespace BdoTimers.Core.Model;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height);

public sealed record AppSettings
{
    /// <summary>Off until the user turns it on in Settings.</summary>
    public bool Autostart { get; init; }
    public float Volume { get; init; } = 0.8f;
    /// <summary>Sound key played for timers whose sound is "Default"; see <see cref="Sounds.SoundKeys"/>.</summary>
    public string AlertSound { get; init; } = BuiltInSounds.Default;
    /// <summary>A Kokoro voice id from the speech channel; null, or a voice that no longer exists, means the default.</summary>
    public string? TtsVoice { get; init; }
    public int TtsRate { get; init; }
    public IReadOnlyList<int> DefaultLeadTimesMinutes { get; init; } = AlertConfig.StandardLeadTimesMinutes;
    public double? OverlayLeft { get; init; }
    public double? OverlayTop { get; init; }
    /// <summary>DateTimeOffset.MaxValue means paused until resumed.</summary>
    public DateTimeOffset? AlertsPausedUntilUtc { get; init; }
    /// <summary>Set once the priority notifications hint was shown for the app id notifications use now; the flag
    /// before it (priorityHintShown) was for the Windows App SDK's id, which Windows' priority list no longer matches.</summary>
    public bool NotificationHintShown { get; init; }
    public WindowPlacement? Window { get; init; }
}
