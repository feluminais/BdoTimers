namespace BdoTimers.Core.Model;

/// <summary>What the Schedule's Month shows.</summary>
public sealed record CalendarSettings
{
    public bool ShowBosses { get; init; } = true;
    /// <summary>Weekly timers and running countdowns.</summary>
    public bool ShowTimers { get; init; } = true;
    public bool ShowEvents { get; init; } = true;
    /// <summary>The daily and weekly to-do resets.</summary>
    public bool ShowResets { get; init; } = true;
}
