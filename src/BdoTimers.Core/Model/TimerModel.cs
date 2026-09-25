namespace BdoTimers.Core.Model;

public enum TimerKind { Scheduled, Countdown, Stopwatch }

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
    /// <summary>When the current run was started; kept through pause and resume.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }
}

/// <summary>Counts up from when it was started; it has no end, so it never alerts.</summary>
public sealed record StopwatchSpec
{
    public CountdownStatus Status { get; init; } = CountdownStatus.Idle;
    /// <summary>While running: now minus this is the time counted, so resuming after a pause moves it forward.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }
    /// <summary>While paused: the time counted so far.</summary>
    public TimeSpan? Elapsed { get; init; }
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
    public StopwatchSpec? Stopwatch { get; init; }
    public AlertConfig Alerts { get; init; } = new();
    /// <summary>Custom picture's file name inside the app's images folder; built-in bosses and presets use bundled art.</summary>
    public string? ImageFile { get; init; }
    /// <summary>Which of <see cref="Seed.Presets"/> this timer is; presets can't be deleted.</summary>
    public string? Preset { get; init; }
}
