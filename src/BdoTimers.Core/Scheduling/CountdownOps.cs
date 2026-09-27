using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class CountdownOps
{
    public static CountdownSpec Start(CountdownSpec c, DateTimeOffset now) => StartFrom(c, now);

    /// <summary>Runs as if started at <paramref name="startedAtUtc"/>, for a countdown started late or not at all.</summary>
    public static CountdownSpec StartFrom(CountdownSpec c, DateTimeOffset startedAtUtc) =>
        c with { Status = CountdownStatus.Running, EndsAtUtc = startedAtUtc + c.Duration, Remaining = null, StartedAtUtc = startedAtUtc };

    /// <summary>When a countdown <paramref name="percent"/> % of the way through <paramref name="duration"/> at
    /// <paramref name="now"/> started, e.g. a crop showing that much growth.</summary>
    public static DateTimeOffset StartForProgress(TimeSpan duration, int percent, DateTimeOffset now) =>
        now - TimeSpan.FromTicks(duration.Ticks * percent / 100);

    /// <summary>The whole percent of <paramref name="duration"/> that <paramref name="elapsed"/> covers, rounded down
    /// like the game's growth display and kept within 0-100.</summary>
    public static int ProgressPercent(TimeSpan duration, TimeSpan elapsed) =>
        (int)Math.Clamp(elapsed.Ticks * 100 / duration.Ticks, 0, 100);

    public static CountdownSpec Pause(CountdownSpec c, DateTimeOffset now) =>
        c is { Status: CountdownStatus.Running, EndsAtUtc: { } end }
            ? c with
            {
                Status = CountdownStatus.Paused,
                EndsAtUtc = null,
                Remaining = end > now ? end - now : TimeSpan.Zero,
            }
            : c;

    public static CountdownSpec Resume(CountdownSpec c, DateTimeOffset now) =>
        c is { Status: CountdownStatus.Paused, Remaining: { } left }
            ? c with { Status = CountdownStatus.Running, EndsAtUtc = now + left, Remaining = null }
            : c;

    public static CountdownSpec Reset(CountdownSpec c) =>
        c with { Status = CountdownStatus.Idle, EndsAtUtc = null, Remaining = null, StartedAtUtc = null };

    /// <summary>A finished countdown goes back to Ready.</summary>
    public static CountdownSpec Complete(CountdownSpec c) => Reset(c);
}
