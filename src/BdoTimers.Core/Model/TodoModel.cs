namespace BdoTimers.Core.Model;

public enum TodoCadence { Daily, Weekly }
public enum TodoCheck { Open, Partial, Done }

public sealed record TodoSchedule
{
    public TodoCadence Cadence { get; init; }
    public DayOfWeek Day { get; init; } = DayOfWeek.Thursday;
    public int Hour { get; init; }
    public int Minute { get; init; }
    public bool LocalTime { get; init; }

    public static TodoSchedule DailyDefault { get; } = new() { Cadence = TodoCadence.Daily };
    public static TodoSchedule WeeklyDefault { get; } = new() { Cadence = TodoCadence.Weekly };
}

public sealed record TodoRow
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; init; } = "";
    public bool Done { get; init; }
    public IReadOnlyList<TodoRow> Children { get; init; } = [];
}

public sealed record TodoList
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public TodoCadence Cadence { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool Enabled { get; init; }
    public bool Deleted { get; init; }
    public TodoSchedule Schedule { get; init; } = TodoSchedule.DailyDefault;
    public DateTimeOffset NextResetUtc { get; init; }
    public IReadOnlyList<TodoRow> Rows { get; init; } = [];
}

public sealed record TodoData
{
    public IReadOnlyList<TodoList> Lists { get; init; } = [];
}
