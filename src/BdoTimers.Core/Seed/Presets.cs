using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <summary>
/// Timers everyone starts with at the top of the Timers screen, in this order: crops growing, time spent fishing, and
/// the wait before a horse registered on the Horse Market goes on sale.
/// </summary>
public static class Presets
{
    public const string Farm = "farm";
    public const string Fishing = "fishing";
    public const string HorseRegistration = "horse-registration";
    public const string HorseRegistrationRun = "horse-registration-run";

    static readonly string[] Order = [Farm, Fishing, HorseRegistration];

    /// <summary>Default temperature estimate; offline time and crop care can delay the harvest.</summary>
    public static readonly TimeSpan CropGrowth = TimeSpan.FromHours(22);

    /// <summary>From the game's notice that a horse was registered on the Horse Market until the horse goes on sale.</summary>
    public static readonly TimeSpan HorseMarketWait = TimeSpan.FromMinutes(10);

    /// <summary>The presets that can't be deleted; <see cref="Ensure"/> adds them back.</summary>
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

    /// <summary>
    /// Can be deleted like the user's own timers, so only new data starts with it (<see cref="SeedService.NewData"/>);
    /// <see cref="Ensure"/> doesn't add it back.
    /// </summary>
    public static TimerDef CreateHorseRegistration() => new()
    {
        Name = "Horse registration",
        Kind = TimerKind.Countdown,
        Preset = HorseRegistration,
        Countdown = new CountdownSpec { Duration = HorseMarketWait },
        // Its own alert times: the default's 15 minutes would alert the moment a 10-minute countdown starts.
        Alerts = new AlertConfig { LeadTimesMinutes = [1, 0] },
    };

    /// <summary>Adds any preset that can't be deleted and the data lacks, ahead of the other timers.</summary>
    public static AppData Ensure(AppData data)
    {
        var missing = Create().Where(p => data.Timers.All(t => t.Preset != p.Preset)).ToList();
        return missing.Count == 0 ? data : data with { Timers = [.. missing, .. data.Timers] };
    }

    /// <summary>Farm crops keep growing after the harvest time, so its countdown runs on past zero until reset.</summary>
    public static bool Overgrows(string? preset) => preset == Farm;

    /// <summary>Farm and Fishing can't be deleted; Horse registration and the user's own timers can.</summary>
    public static bool CanDelete(string? preset) => preset is null or HorseRegistration or HorseRegistrationRun;

    /// <summary>Sort key that puts presets first, in their fixed order, and keeps other timers after them.</summary>
    public static int Rank(string? preset) => preset == HorseRegistrationRun ? Array.IndexOf(Order, HorseRegistration)
        : preset is null ? Order.Length : Math.Max(0, Array.IndexOf(Order, preset));
}
