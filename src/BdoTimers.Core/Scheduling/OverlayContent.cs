using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

/// <summary>What the overlay draws; a switched-off section is null or empty.</summary>
public sealed record OverlaySnapshot(
    bool Clock,
    SpawnGroup? Previous,
    SpawnGroup? Next,
    IReadOnlyList<UpcomingItem> PopUps,
    TimeSpan? FarmLeft,
    TimeSpan? FishingElapsed,
    IReadOnlyList<HorseOverlayRun> HorseRegistrations,
    int MoreHorseRegistrations)
{
    public int? FarmProgress { get; init; }
    /// <summary>Includes due occurrences rendered by another section instead of a separate pop-up row.</summary>
    public bool HasDuePopUp { get => _hasDuePopUp || PopUps.Count > 0; init => _hasDuePopUp = value; }
    readonly bool _hasDuePopUp;

    /// <summary>True when there is nothing to draw, not even the clock.</summary>
    public bool IsEmpty =>
        !Clock && Previous is null && Next is null && PopUps.Count == 0 && FarmLeft is null
        && FishingElapsed is null && HorseRegistrations.Count == 0;
}

/// <summary>A running horse registration; <see cref="Id"/> is its timer's.</summary>
public sealed record HorseOverlayRun(Guid Id, string Name, DateTimeOffset EndsAtUtc);

public static class OverlayContent
{
    /// <param name="boards">Shares the boss board with other callers; without it the board is built afresh.</param>
    public static OverlaySnapshot Build(AppData data, OverlaySettings settings, DateTimeOffset now, BossBoardCache? boards = null)
    {
        var board = settings.ShowPrevious || settings.ShowNext ? boards?.Get(data, now) ?? BossBoard.Build(data, now) : null;
        var next = settings.ShowNext ? board?.Next : null;
        var farm = settings.ShowFarm ? FarmLeft(data, now) : null;
        // Running registrations only: a paused one has no end to count down to.
        var horse = settings.ShowHorseRegistrations
            ? data.Timers.Where(t => t.Preset == Presets.HorseRegistrationRun
                && t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } && end >= now)
                .OrderByDescending(t => t.Countdown!.StartedAtUtc)
                .ThenByDescending(t => t.Id)
                .ToList()
            : [];
        // An occurrence the overlay already shows gets no second row.
        var due = UpcomingQuery.ForOverlay(data, now);
        var popUps = due
            .Where(i => !(next is not null && i.AtUtc == next.AtUtc && next.Bosses.Any(b => b.Id == i.Timer.Id)))
            .Where(i => !(farm is not null && i.Timer.Preset == Presets.Farm))
            .Where(i => !(settings.ShowHorseRegistrations && i.Timer.Preset == Presets.HorseRegistrationRun))
            .ToList();
        return new OverlaySnapshot(
            settings.ShowClock,
            settings.ShowPrevious ? board?.Previous : null,
            next,
            popUps,
            farm,
            settings.ShowFishing ? FishingElapsed(data, now) : null,
            horse.Take(2).Select(t => new HorseOverlayRun(
                t.Id, t.HorseRunNumber is { } number ? $"Horse {number}" : t.Name, t.Countdown!.EndsAtUtc!.Value)).ToList(),
            Math.Max(0, horse.Count - 2))
        {
            FarmProgress = settings.ShowFarm ? FarmProgress(data, now) : null,
            HasDuePopUp = due.Count > 0,
        };
    }

    /// <summary>The Farm preset's signed time left while its countdown runs or is paused.</summary>
    static TimeSpan? FarmLeft(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Farm)?.Countdown is { } countdown
            ? CountdownOps.Left(countdown, now) : null;

    static int? FarmProgress(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Farm)?.Countdown is { } countdown
            ? CountdownOps.Progress(countdown, now, overgrows: true) : null;

    /// <summary>The Fishing preset's counted time while its stopwatch runs or is paused.</summary>
    static TimeSpan? FishingElapsed(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Fishing)?.Stopwatch is { Status: not CountdownStatus.Idle } s
            ? StopwatchOps.Elapsed(s, now)
            : null;
}
