namespace BdoTimers.Core.Model;

public enum TimerKind { Scheduled, Countdown, Stopwatch, OneTime }

public enum CountdownStatus { Idle, Running, Paused }

/// <param name="Label">Names this time within its timer, such as "Battle"; alerts and lists show it after the timer's name.</param>
public readonly record struct Slot(DayOfWeek Day, TimeOnly Time, string? Label = null);

public sealed record ScheduledSpec
{
    /// <summary>A Windows or IANA time zone id; .NET resolves IANA ids on Windows through ICU.</summary>
    public string TimeZoneId { get; init; } = "UTC";
    public IReadOnlyList<Slot> Slots { get; init; } = [];
    /// <summary>Inclusive limits on the slot's date in this schedule's time zone.</summary>
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    /// <summary>1 for every week; 2 for every other week, and so on, counted in Monday-first weeks from
    /// <see cref="WeekAnchor"/>'s.</summary>
    public int EveryWeeks { get; init; } = 1;
    /// <summary>A date in a week the schedule runs, when <see cref="EveryWeeks"/> is over 1; null counts from
    /// <see cref="StartDate"/>, else from 2001-01-01.</summary>
    public DateOnly? WeekAnchor { get; init; }
}

public sealed record OneTimeSpec
{
    public DateOnly Date { get; init; }
    public TimeOnly Time { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    /// <summary>Saved before a missed notice is issued; clock rollback and restart cannot rearm the event.</summary>
    public bool Finished { get; init; }
    /// <summary>Distinguishes an explicit reschedule from fired leads of an earlier use of the same UTC instant.</summary>
    public Guid ScheduleVersion { get; init; } = Guid.NewGuid();
}

public sealed record CountdownSpec
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(60);
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
    /// <summary>A world boss of <see cref="BossRegionId"/>: from the bundled timetable, or one the player added.</summary>
    public bool IsBuiltIn { get; init; }
    /// <summary>A boss the player added; bundled timetable updates and resets keep it unless it takes a bundled boss's name.</summary>
    public bool AddedByUser { get; init; }
    /// <summary>Built-in boss region; null in legacy files means EU. Custom schedules ignore this field.</summary>
    public string? BossRegionId { get; init; }
    public ScheduledSpec? Scheduled { get; init; }
    public CountdownSpec? Countdown { get; init; }
    public StopwatchSpec? Stopwatch { get; init; }
    public OneTimeSpec? OneTime { get; init; }
    public AlertConfig Alerts { get; init; } = new();
    /// <summary>Starts a new registration from the Horse registration preset.</summary>
    public Hotkey? StartHotkey { get; init; }
    /// <summary>Starts, pauses or resumes a user-created countdown; reset is an explicit UI action.</summary>
    public Hotkey? ControlHotkey { get; init; }
    /// <summary>Slot number of an independently running Horse registration.</summary>
    public int? HorseRunNumber { get; init; }
    /// <summary>Custom picture's file name inside the app's images folder; built-in bosses and presets use bundled art.</summary>
    public string? ImageFile { get; init; }
    /// <summary>Which of <see cref="Seed.Presets"/> this timer is; Farm and Fishing can't be deleted.</summary>
    public string? Preset { get; init; }
}
