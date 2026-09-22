namespace BdoTimers.Core.Model;

public enum TimerKind { Scheduled, Countdown }

public enum CountdownStatus { Idle, Running, Paused }

public readonly record struct Slot(DayOfWeek Day, TimeOnly Time);

public sealed record ScheduledSpec
{
    public string TimeZoneId { get; init; } = "UTC";
    public IReadOnlyList<Slot> Slots { get; init; } = [];
}

public sealed record CountdownSpec
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(60);
    public bool AutoRepeat { get; init; }
    public CountdownStatus Status { get; init; } = CountdownStatus.Idle;
    public DateTimeOffset? EndsAtUtc { get; init; }
    public TimeSpan? Remaining { get; init; }
}

public sealed record TimerDef
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public TimerKind Kind { get; init; }
    public bool Enabled { get; init; } = true;
    public bool IsBuiltIn { get; init; }
    public ScheduledSpec? Scheduled { get; init; }
    public CountdownSpec? Countdown { get; init; }
    public AlertConfig Alerts { get; init; } = new();
}
