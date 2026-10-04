namespace BdoTimers.Core.Model;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height);

public sealed record AppSettings
{
    /// <summary>Off until the user turns it on in Settings.</summary>
    public bool Autostart { get; init; }
    /// <summary>Opt in to keeping timers and alerts running when the main window is closed.</summary>
    public bool CloseToTray { get; init; }
    public double TextScale { get; init; } = 1;
    public float Volume { get; init; } = 0.8f;
    /// <summary>Sound key played for timers whose sound is "Default"; see <see cref="Sounds.SoundKeys"/>.</summary>
    public string AlertSound { get; init; } = BuiltInSounds.Default;
    /// <summary>A Kokoro voice id from the speech channel; null, or a voice that no longer exists, means the default.</summary>
    public string? TtsVoice { get; init; }
    public int TtsRate { get; init; }
    public IReadOnlyList<int> DefaultLeadTimesMinutes { get; init; } = AlertConfig.StandardLeadTimesMinutes;
    public double? OverlayLeft { get; init; }
    public double? OverlayTop { get; init; }
    public OverlaySettings Overlay { get; init; } = new();
    /// <summary>DateTimeOffset.MaxValue means paused until resumed.</summary>
    public DateTimeOffset? AlertsPausedUntilUtc { get; init; }
    /// <summary>Set once the hint to add the app to Windows' priority notifications has been shown.</summary>
    public bool NotificationHintShown { get; init; }
    /// <summary>Legacy EU notice revision, copied into its region profile by migration.</summary>
    public string? TimetableNoticeRevision { get; init; }
    public WindowPlacement? Window { get; init; }
    public TodoSchedule DailyTodoReset { get; init; } = new();
    public TodoSchedule WeeklyTodoReset { get; init; } = new();
    public CalendarSettings Calendar { get; init; } = new();
}
