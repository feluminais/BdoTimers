using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>
/// Garmoth can be done three times a week, though he spawns far more often. With the tracker on, the player marks each kill,
/// and from the third until the weekly reset his spawns count for nothing: they behave as if he were not followed.
/// </summary>
public static class GarmothTracker
{
    public const int Limit = 3;
    public const string BossName = "Garmoth";

    /// <summary>The bundled Garmoth of any region.</summary>
    public static bool IsGarmoth(TimerDef timer) => timer.IsBuiltIn && !timer.AddedByUser && timer.Name == BossName;

    /// <summary>Whether this spawn of <paramref name="timer"/> is out, Garmoth having been done for the week.</summary>
    public static bool Gone(AppData data, TimerDef timer, DateTimeOffset atUtc) =>
        data.Garmoth is { Kills: >= Limit, DoneAtUtc: { } from } week && atUtc >= from && atUtc < week.ResetUtc && IsGarmoth(timer);

    /// <summary>
    /// The skipped spawns and the Garmoth spawns that are gone: what alerts and pop-ups leave out. The spawns of a week that
    /// is not done are not looked up.
    /// </summary>
    public static HashSet<MutedOccurrence> Silenced(AppData data)
    {
        var silenced = data.Muted.ToHashSet();
        if (data.Garmoth is not { Kills: >= Limit, DoneAtUtc: { } from } week) return silenced;
        foreach (var timer in data.Timers.Where(t => IsGarmoth(t) && t.Scheduled is { Off: false }))
            foreach (var at in ScheduleMath.From(timer.Scheduled!, from).TakeWhile(at => at < week.ResetUtc))
                silenced.Add(new MutedOccurrence(timer.Id, at));
        return silenced;
    }

    /// <summary>
    /// Pressing kill <paramref name="kill"/> (1 to <see cref="Limit"/>) marks it and the kills before it; pressing one that is
    /// marked takes it and the ones after it back. The third marked is the moment Garmoth is gone from.
    /// </summary>
    public static GarmothWeek Mark(GarmothWeek week, int kill, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(kill, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(kill, Limit);
        var kills = week.Kills >= kill ? kill - 1 : kill;
        return week with { Kills = kills, DoneAtUtc = kills >= Limit ? nowUtc : null };
    }

    /// <summary>
    /// The week as it should be at <paramref name="nowUtc"/>: empty with the tracker off, empty once its reset has passed,
    /// and ending at the next weekly reset in <paramref name="weeklyReset"/>, so a changed schedule applies without clearing
    /// the kills.
    /// </summary>
    public static GarmothWeek Reconcile(GarmothWeek week, bool tracking, TodoSchedule weeklyReset, DateTimeOffset nowUtc)
    {
        if (!tracking) return new GarmothWeek();
        var due = week.ResetUtc != default && week.ResetUtc <= nowUtc;
        var kept = due ? new GarmothWeek() : week;
        return kept with { ResetUtc = TodoReset.Next(TodoCadence.Weekly, weeklyReset, nowUtc) };
    }
}
