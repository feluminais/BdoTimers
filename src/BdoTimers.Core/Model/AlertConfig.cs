namespace BdoTimers.Core.Model;

public sealed record SoundAlert
{
    public bool Enabled { get; init; } = true;
    /// <summary>A sound key (see <see cref="Sounds.SoundKeys"/>); null plays the app-wide alert sound from settings.</summary>
    public string? Key { get; init; }
}

public sealed record ToastAlert
{
    public bool Enabled { get; init; } = true;
}

public sealed record TtsAlert
{
    public bool Enabled { get; init; } = true;
    public string Template { get; init; } = "{name} in {duration}";
    public string NowTemplate { get; init; } = "{name} now";
}

public sealed record OverlayAlert
{
    public bool Enabled { get; init; }
    public int ShowMinutesBefore { get; init; } = 5;
}

public sealed record AlertConfig
{
    public static readonly IReadOnlyList<int> StandardLeadTimesMinutes = [15, 5, 1, 0];

    /// <summary>Minutes before the event; 0 means at the event. Null follows the default alert times in Settings.</summary>
    public IReadOnlyList<int>? LeadTimesMinutes { get; init; }
    public SoundAlert Sound { get; init; } = new();
    public ToastAlert Toast { get; init; } = new();
    public TtsAlert Tts { get; init; } = new();
    public OverlayAlert Overlay { get; init; } = new();

    /// <summary>The alert times in effect: the timer's own, else <paramref name="defaults"/>.</summary>
    public IReadOnlyList<int> LeadTimes(IReadOnlyList<int> defaults) => LeadTimesMinutes ?? defaults;

    /// <summary>True when the timer has its own alert times or sound, so changing the defaults in Settings won't change it.</summary>
    public bool OverridesDefaults => LeadTimesMinutes is not null || Sound.Key is not null || !Sound.Enabled;
}
