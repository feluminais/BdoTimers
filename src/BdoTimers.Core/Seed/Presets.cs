using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <summary>Timers everyone has at the top of the Timers screen, in this order: crops growing, and time spent fishing.</summary>
public static class Presets
{
    public const string Farm = "farm";
    public const string Fishing = "fishing";

    static readonly string[] Order = [Farm, Fishing];

    /// <summary>Crops take 20 to 22 hours depending on temperature (June 4, 2026 update); at 22 they are ready anywhere.</summary>
    public static readonly TimeSpan CropGrowth = TimeSpan.FromHours(22);

    public static IReadOnlyList<TimerDef> Create() =>
    [
        new TimerDef
        {
            Name = "Farm",
            Kind = TimerKind.Countdown,
            Preset = Farm,
            Countdown = new CountdownSpec { Duration = CropGrowth },
        },
        new TimerDef
        {
            Name = "Fishing",
            Kind = TimerKind.Stopwatch,
            Preset = Fishing,
            Stopwatch = new StopwatchSpec(),
        },
    ];

    /// <summary>Adds any preset the data lacks, ahead of the other timers.</summary>
    public static AppData Ensure(AppData data)
    {
        var missing = Create().Where(p => data.Timers.All(t => t.Preset != p.Preset)).ToList();
        return missing.Count == 0 ? data : data with { Timers = [.. missing, .. data.Timers] };
    }

    /// <summary>Sort key that puts presets first, in their fixed order, and keeps other timers after them.</summary>
    public static int Rank(string? preset) => preset is null ? Order.Length : Math.Max(0, Array.IndexOf(Order, preset));
}
