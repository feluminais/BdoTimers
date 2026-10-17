namespace BdoTimers.Core.Model;

/// <summary>Which panels the Today screen shows.</summary>
public sealed record TodaySettings
{
    /// <summary>The timers that are running or paused. With none, the panel is gone whatever this says.</summary>
    public bool ShowCurrentTimers { get; init; } = true;
    public bool ShowDaily { get; init; } = true;
    public bool ShowWeekly { get; init; } = true;
    /// <summary>Only matters while <see cref="AppSettings.GarmothTracker"/> is on.</summary>
    public bool ShowGarmoth { get; init; } = true;
    /// <summary>The real time until night falls in game, or by night until day breaks.</summary>
    public bool ShowNight { get; init; }
}
