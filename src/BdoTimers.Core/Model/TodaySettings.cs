namespace BdoTimers.Core.Model;

/// <summary>Which panels the Today screen shows.</summary>
public sealed record TodaySettings
{
    public bool ShowDaily { get; init; } = true;
    public bool ShowWeekly { get; init; } = true;
    /// <summary>Only matters while <see cref="AppSettings.GarmothTracker"/> is on.</summary>
    public bool ShowGarmoth { get; init; } = true;
}
