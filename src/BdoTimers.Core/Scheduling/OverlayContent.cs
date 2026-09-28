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
    TimeSpan? FishingElapsed)
{
    /// <summary>Includes due occurrences rendered by another section instead of a separate pop-up row.</summary>
    public bool HasDuePopUp { get => _hasDuePopUp || PopUps.Count > 0; init => _hasDuePopUp = value; }
    readonly bool _hasDuePopUp;

    /// <summary>True when there is nothing to draw, not even the clock.</summary>
    public bool IsEmpty =>
        !Clock && Previous is null && Next is null && PopUps.Count == 0 && FarmLeft is null && FishingElapsed is null;
}

public static class OverlayContent
{
    public static OverlaySnapshot Build(AppData data, OverlaySettings settings, DateTimeOffset now)
    {
        var board = settings.ShowPrevious || settings.ShowNext ? BossBoard.Build(data, now) : null;
        var next = settings.ShowNext ? board?.Next : null;
        var farm = settings.ShowFarm ? FarmLeft(data, now) : null;
        // An occurrence the overlay already shows gets no second row.
        var due = UpcomingQuery.ForOverlay(data, now);
        var popUps = due
            .Where(i => !(next is not null && i.AtUtc == next.AtUtc && next.Bosses.Any(b => b.Id == i.Timer.Id)))
            .Where(i => !(farm is not null && i.Timer.Preset == Presets.Farm))
            .ToList();
        return new OverlaySnapshot(
            settings.ShowClock,
            settings.ShowPrevious ? board?.Previous : null,
            next,
            popUps,
            farm,
            settings.ShowFishing ? FishingElapsed(data, now) : null) { HasDuePopUp = due.Count > 0 };
    }

    /// <summary>The Farm preset's time left while its countdown runs or is paused.</summary>
    static TimeSpan? FarmLeft(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Farm)?.Countdown switch
        {
            { Status: CountdownStatus.Running, EndsAtUtc: { } end } => end > now ? end - now : TimeSpan.Zero,
            { Status: CountdownStatus.Paused, Remaining: { } left } => left,
            _ => null,
        };

    /// <summary>The Fishing preset's counted time while its stopwatch runs or is paused.</summary>
    static TimeSpan? FishingElapsed(AppData data, DateTimeOffset now) =>
        data.Timers.FirstOrDefault(t => t.Preset == Presets.Fishing)?.Stopwatch is { Status: not CountdownStatus.Idle } s
            ? StopwatchOps.Elapsed(s, now)
            : null;
}
