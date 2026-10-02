using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

/// <summary>What the overlay draws; a switched-off section is null or empty.</summary>
public sealed record OverlaySnapshot
{
    public bool Clock { get; init; }
    public SpawnGroup? Previous { get; init; }
    public SpawnGroup? Next { get; init; }
    public IReadOnlyList<UpcomingItem> PopUps { get; init; } = [];
    public TimeSpan? FarmLeft { get; init; }
    public TimeSpan? FishingElapsed { get; init; }
    public IReadOnlyList<HorseOverlayRun> HorseRegistrations { get; init; } = [];
    public int MoreHorseRegistrations { get; init; }
    public int? FarmProgress { get; init; }
    public IReadOnlyList<CustomOverlayTimer> CustomTimers { get; init; } = [];
    /// <summary>Includes due occurrences rendered by another section instead of a separate pop-up row.</summary>
    public bool HasDuePopUp { get => _hasDuePopUp || PopUps.Count > 0; init => _hasDuePopUp = value; }
    readonly bool _hasDuePopUp;

    /// <summary>True when there is nothing to draw, not even the clock.</summary>
    public bool IsEmpty =>
        !Clock && Previous is null && Next is null && PopUps.Count == 0 && FarmLeft is null
        && FishingElapsed is null && HorseRegistrations.Count == 0 && CustomTimers.Count == 0;
}

/// <summary>A running horse registration; <see cref="Id"/> is its timer's.</summary>
public sealed record HorseOverlayRun(Guid Id, string Name, DateTimeOffset EndsAtUtc);

public sealed record CustomOverlayTimer(Guid Id, string Name, TimeSpan Remaining, bool Paused);

public static class OverlayContent
{
    /// <param name="boards">Shares the boss board with other callers; without it the board is built afresh.</param>
    public static OverlaySnapshot Build(AppData data, OverlaySettings settings, DateTimeOffset now, BossBoardCache? boards = null)
    {
        var board = settings.ShowPrevious || settings.ShowNext ? boards?.Get(data, now) ?? BossBoard.Build(data, now) : null;
        var next = settings.ShowNext ? board?.Next : null;
        var farm = settings.ShowFarm ? data.Timers.FirstOrDefault(t => t.Preset == Presets.Farm)?.Countdown : null;
        var farmLeft = farm is null ? null : CountdownOps.Left(farm, now);
        // Saved timer order stays stable when times cross, a timer pauses, or its name changes.
        var custom = settings.ShowCustomTimers
            ? data.Timers.Select(t => CustomTimer(t, now)).OfType<CustomOverlayTimer>().ToList() : [];
        var customIds = custom.Select(t => t.Id).ToHashSet();
        var horse = settings.ShowHorseRegistrations
            ? data.Timers.Where(t => t.Preset == Presets.HorseRegistrationRun
                && t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } && end >= now)
                .OrderByDescending(t => t.Countdown!.StartedAtUtc)
                .ThenByDescending(t => t.Id)
                .ToList()
            : [];
        // An occurrence the overlay already shows gets no second row.
        var due = UpcomingQuery.ForOverlay(data, settings, now);
        var popUps = due
            .Where(i => !(next is not null && i.AtUtc == next.AtUtc && next.Bosses.Any(b => b.Id == i.Timer.Id)))
            .Where(i => !(farmLeft is not null && i.Timer.Preset == Presets.Farm))
            .Where(i => !(settings.ShowHorseRegistrations && i.Timer.Preset == Presets.HorseRegistrationRun))
            .Where(i => !customIds.Contains(i.Timer.Id))
            .ToList();
        return new OverlaySnapshot
        {
            Clock = settings.ShowClock,
            Previous = settings.ShowPrevious ? board?.Previous : null,
            Next = next,
            PopUps = popUps,
            FarmLeft = farmLeft,
            FarmProgress = farm is null ? null : CountdownOps.Progress(farm, now, overgrows: true),
            FishingElapsed = settings.ShowFishing ? FishingElapsed(data, now) : null,
            HorseRegistrations = horse.Take(2).Select(t => new HorseOverlayRun(
                t.Id, t.HorseRunNumber is { } number ? $"Horse {number}" : t.Name, t.Countdown!.EndsAtUtc!.Value)).ToList(),
            MoreHorseRegistrations = Math.Max(0, horse.Count - 2),
            CustomTimers = custom,
            HasDuePopUp = due.Count > 0,
        };
    }

    static CustomOverlayTimer? CustomTimer(TimerDef timer, DateTimeOffset now)
    {
        if (timer is { IsBuiltIn: false, Kind: TimerKind.OneTime, OneTime: { Finished: false } oneTime })
        {
            var at = OneTimeEvents.AtUtc(oneTime);
            return at >= now ? new(timer.Id, timer.Name, at - now, false) : null;
        }
        if (!CustomCountdowns.Includes(timer)) return null;
        return timer.Countdown switch
        {
            { Status: CountdownStatus.Running, EndsAtUtc: { } end } when end >= now => new(timer.Id, timer.Name, end - now, false),
            { Status: CountdownStatus.Paused, Remaining: { } left } => new(timer.Id, timer.Name, left, true),
            _ => null,
        };
    }

    /// <summary>The Fishing preset's counted time while its stopwatch runs or is paused.</summary>
    static TimeSpan? FishingElapsed(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Fishing)?.Stopwatch is { Status: not CountdownStatus.Idle } s
            ? StopwatchOps.Elapsed(s, now)
            : null;
}
