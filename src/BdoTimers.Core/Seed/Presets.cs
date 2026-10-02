using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <summary>
/// Timers everyone starts with at the top of the Timers screen: farm growth, fishing, horse registration, and guild events.
/// </summary>
public static class Presets
{
    public const string Farm = "farm";
    public const string Fishing = "fishing";
    public const string HorseRegistration = "horse-registration";
    public const string HorseRegistrationRun = "horse-registration-run";
    public const string GuildBosses = "guild-bosses";
    public const string GuildWar = "guild-war";

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
        new TimerDef
        {
            Name = "Guild bosses",
            Kind = TimerKind.Scheduled,
            Preset = GuildBosses,
            Scheduled = new ScheduledSpec { TimeZoneId = TimeZoneInfo.Local.Id },
        },
        new TimerDef
        {
            Name = "Guild war",
            Kind = TimerKind.Scheduled,
            Preset = GuildWar,
            Scheduled = new ScheduledSpec { TimeZoneId = TimeZoneInfo.Local.Id },
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
        StartHotkey = DefaultHotkeys.HorseRegistration,
        Countdown = new CountdownSpec { Duration = HorseMarketWait },
        // Its own alert times: the default's 15 minutes would alert the moment a 10-minute countdown starts.
        Alerts = new AlertConfig { LeadTimesMinutes = [1, 0] },
    };

    /// <summary>A registration started from the Horse registration preset: its copy numbered <paramref name="number"/>,
    /// without the preset's hotkey or picture.</summary>
    public static TimerDef HorseRun(TimerDef template, int number) => template with
    {
        Id = Guid.NewGuid(), Name = $"{template.Name} {number}", Preset = HorseRegistrationRun,
        HorseRunNumber = number, StartHotkey = null, ImageFile = null,
    };

    /// <summary>A horse registration that is running or paused; these count towards the limit of runs.</summary>
    public static bool IsActiveHorseRun(TimerDef timer) =>
        timer.Preset == HorseRegistrationRun && timer.Countdown?.Status is not CountdownStatus.Idle;

    /// <summary>Adds any preset that can't be deleted and the data lacks, ahead of the other timers.</summary>
    public static AppData Ensure(AppData data)
    {
        var missing = Create().Where(p => data.Timers.All(t => t.Preset != p.Preset)).ToList();
        return missing.Count == 0 ? data : data with { Timers = missing.Concat(data.Timers).OrderBy(t => Rank(t.Preset)).ToList() };
    }

    /// <summary>Farm crops keep growing after the harvest time, so its countdown runs on past zero until reset.</summary>
    public static bool Overgrows(string? preset) => preset == Farm;

    /// <summary>Horse registration and the user's own timers can be deleted.</summary>
    public static bool CanDelete(string? preset) => preset is null or HorseRegistration or HorseRegistrationRun;

    /// <summary>Sort key that puts presets first, in their fixed order, and keeps other timers after them.</summary>
    public static int Rank(string? preset) => preset switch
    {
        Farm => 0,
        Fishing => 1,
        HorseRegistration or HorseRegistrationRun => 2,
        GuildBosses => 3,
        GuildWar => 4,
        _ => 5,
    };

    /// <summary>Guild presets may be unset; other weekly timers keep a time.</summary>
    public static int MinimumSlots(string? preset) => preset is GuildBosses or GuildWar ? 0 : 1;

    /// <summary>Guild bosses has one weekly event; Guild war may be scheduled more often.</summary>
    public static int? MaximumSlots(string? preset) => preset == GuildBosses ? 1 : null;
}
