namespace BdoTimers.Core.Model;

public enum TodoCadence { Daily, Weekly }
public enum TodoCheck { Open, Partial, Done }

/// <summary>When every list of a cadence resets; <see cref="Day"/> applies to weekly lists only.</summary>
public sealed record TodoSchedule
{
    public DayOfWeek Day { get; init; } = DayOfWeek.Thursday;
    public int Hour { get; init; }
    public int Minute { get; init; }
    public bool LocalTime { get; init; }
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
    public DateTimeOffset NextResetUtc { get; init; }
    public IReadOnlyList<TodoRow> Rows { get; init; } = [];
}

[Storage.SavedVersion(nameof(TodoData.DefaultsVersion), TodoData.CurrentDefaultsVersion)]
public sealed record TodoData
{
    public const int CurrentDefaultsVersion = 2;
    public int DefaultsVersion { get; init; }
    public IReadOnlyList<TodoList> Lists { get; init; } = [];
}
