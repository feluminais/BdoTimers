using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <summary>
/// Timers everyone starts with at the top of the Timers screen: farm growth, fishing, horse registration and guild bosses.
/// </summary>
public static class Presets
{
    public const string Farm = "farm";
    public const string Fishing = "fishing";
    public const string HorseRegistration = "horse-registration";
    public const string HorseRegistrationRun = "horse-registration-run";
    public const string GuildBosses = "guild-bosses";
    /// <summary>No longer a preset; data from earlier versions may keep one that has times set.</summary>
    public const string GuildWar = "guild-war";
    public const string WarOfTheRoses = "war-of-the-roses";

    /// <summary>A War of the Roses Sunday in both regions; it runs every other week from there.</summary>
    public static readonly DateOnly WarOfTheRosesWeek = new(2026, 9, 20);

    /// <summary>Default temperature estimate; offline time and crop care can delay the harvest.</summary>
    public static readonly TimeSpan CropGrowth = TimeSpan.FromHours(22);

    /// <summary>From the game's notice that a horse was registered on the Horse Market until the horse goes on sale.</summary>
    public static readonly TimeSpan HorseMarketWait = TimeSpan.FromMinutes(10);

    /// <summary>The weekly time Guild bosses starts with, turned off until the player sets their guild's time.</summary>
    public static readonly Slot GuildBossesTime = new(DayOfWeek.Monday, new TimeOnly(20, 0));

    /// <summary>The presets that can't be deleted; <see cref="Ensure"/> adds them back. War of the Roses follows
    /// <paramref name="regionId"/>'s server times.</summary>
    public static IReadOnlyList<TimerDef> Create(string regionId = BossRegions.Europe) =>
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
            Scheduled = new ScheduledSpec { TimeZoneId = TimeZoneInfo.Local.Id, Slots = [GuildBossesTime], Off = true },
        },
        new TimerDef
        {
            Name = "War of the Roses",
            Kind = TimerKind.Scheduled,
            Preset = WarOfTheRoses,
            Scheduled = WarOfTheRosesSchedule(regionId),
        },
    ];

    /// <summary>The region's War of the Roses: its applications deadline and battle, every other Sunday.</summary>
    public static ScheduledSpec WarOfTheRosesSchedule(string regionId)
    {
        var region = BossRegions.Find(regionId);
        return new ScheduledSpec
        {
            TimeZoneId = region.TimeZoneId, Slots = region.WarOfTheRoses, EveryWeeks = 2, WeekAnchor = WarOfTheRosesWeek,
        };
    }

    /// <summary>Whether <paramref name="spec"/> is still <paramref name="regionId"/>'s War of the Roses as bundled.</summary>
    public static bool IsDefaultWarOfTheRoses(ScheduledSpec? spec, string regionId)
    {
        var bundled = WarOfTheRosesSchedule(regionId);
        return spec is not null && spec.TimeZoneId == bundled.TimeZoneId && spec.EveryWeeks == bundled.EveryWeeks
            && spec.WeekAnchor == bundled.WeekAnchor && spec.StartDate is null && spec.EndDate is null
            && spec.Slots.ToHashSet().SetEquals(bundled.Slots);
    }

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

    /// <summary>
    /// Adds any preset that can't be deleted and the data lacks, ahead of the other timers, and gives a Guild bosses
    /// timer saved without a time its starting time, turned off.
    /// </summary>
    public static AppData Ensure(AppData data)
    {
        var timers = data.Timers.Select(t => t is { Preset: GuildBosses, Scheduled: { Slots.Count: 0 } spec }
            ? t with { Scheduled = spec with { Slots = [GuildBossesTime], Off = true } }
            : t).ToList();
        var missing = Create(data.SelectedBossRegion).Where(p => timers.All(t => t.Preset != p.Preset)).ToList();
        if (missing.Count > 0) return data with { Timers = missing.Concat(timers).OrderBy(t => Rank(t.Preset)).ToList() };
        return timers.SequenceEqual(data.Timers) ? data : data with { Timers = timers };
    }

    /// <summary>Farm crops keep growing after the harvest time, so its countdown runs on past zero until reset.</summary>
    public static bool Overgrows(string? preset) => preset == Farm;

    /// <summary>Horse registration, a kept Guild war and the user's own timers can be deleted.</summary>
    public static bool CanDelete(string? preset) => preset is null or HorseRegistration or HorseRegistrationRun or GuildWar;

    /// <summary>Sort key that puts presets first, in their fixed order, and keeps other timers after them.</summary>
    public static int Rank(string? preset) => preset switch
    {
        Farm => 0,
        Fishing => 1,
        HorseRegistration or HorseRegistrationRun => 2,
        GuildBosses => 3,
        GuildWar => 4,
        WarOfTheRoses => 5,
        _ => 6,
    };

    /// <summary>Guild war may be unset; other weekly timers keep a time, and Guild bosses is turned off instead.</summary>
    public static int MinimumSlots(string? preset) => preset == GuildWar ? 0 : 1;

    /// <summary>Guild bosses has one weekly event; Guild war may be scheduled more often.</summary>
    public static int? MaximumSlots(string? preset) => preset == GuildBosses ? 1 : null;
}
