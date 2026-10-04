namespace BdoTimers.Core.Model;

public enum OverlayLayout { List, Card, Bar }
public enum OverlayMouseProximity { Off, Fade, Hide }

/// <summary>When the in-game overlay shows and how it looks.</summary>
public sealed record OverlaySettings
{
    public const string DefaultBackgroundColor = "#0B0B0C";

    /// <summary>Off: the overlay never shows, pop-ups included, and its hotkeys are released.</summary>
    public bool Enabled { get; init; } = true;
    public bool AlwaysShow { get; init; }
    /// <summary>Flips <see cref="AlwaysShow"/>.</summary>
    public Hotkey? AlwaysShowHotkey { get; init; } = DefaultHotkeys.AlwaysShow;
    public bool ShowOnHotkey { get; init; } = true;
    /// <summary>Shows the overlay for <see cref="ShowSeconds"/>; held only while <see cref="ShowOnHotkey"/> is on.</summary>
    public Hotkey? ShowHotkey { get; init; } = DefaultHotkeys.Show;
    public int ShowSeconds { get; init; } = 10;
    public OverlayMouseProximity MouseProximity { get; init; }
    public OverlayLayout Layout { get; init; }
    public bool ShowOutline { get; init; } = true;
    /// <summary>0.6 to 2.</summary>
    public double Scale { get; init; } = 1.0;
    /// <summary>The PC's local time.</summary>
    public bool ShowClock { get; init; } = true;
    /// <summary>The selected boss region's server time, beside the local time.</summary>
    public bool ShowServerTime { get; init; }
    /// <summary>The game world's time of day, beside the local time.</summary>
    public bool ShowGameTime { get; init; }
    public bool ShowPrevious { get; init; } = true;
    public bool ShowNext { get; init; } = true;
    public bool ShowFarm { get; init; } = true;
    public bool ShowFishing { get; init; } = true;
    public bool ShowHorseRegistrations { get; init; }
    public bool ShowCustomTimers { get; init; } = true;
    /// <summary>When the Guild bosses timer pops up on the overlay, in place of its own overlay alert; off until turned
    /// on.</summary>
    public OverlayAlert GuildBosses { get; init; } = new();
    /// <summary>"#RRGGBB"; also used when the picture can't be read.</summary>
    public string BackgroundColor { get; init; } = DefaultBackgroundColor;
    /// <summary>A picture's file name inside the app's images folder; it replaces the colour.</summary>
    public string? BackgroundImage { get; init; }
    public double BackgroundOpacity { get; init; } = 0.85;
    /// <summary>0.2 to 1.</summary>
    public double TextOpacity { get; init; } = 1.0;
}
