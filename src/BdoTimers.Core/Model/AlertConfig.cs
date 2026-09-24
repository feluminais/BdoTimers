namespace BdoTimers.Core.Model;

public sealed record SoundAlert
{
    public bool Enabled { get; init; } = true;
    /// <summary>Null plays the app-wide alert sound from settings.</summary>
    public string? FilePath { get; init; }
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

    /// <summary>Minutes before the event; 0 means at the event.</summary>
    public IReadOnlyList<int> LeadTimesMinutes { get; init; } = StandardLeadTimesMinutes;
    public SoundAlert Sound { get; init; } = new();
    public ToastAlert Toast { get; init; } = new();
    public TtsAlert Tts { get; init; } = new();
    public OverlayAlert Overlay { get; init; } = new();
}
