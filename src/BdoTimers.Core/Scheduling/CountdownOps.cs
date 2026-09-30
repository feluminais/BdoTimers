using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class CountdownOps
{
    public static CountdownSpec Start(CountdownSpec c, DateTimeOffset now) => StartFrom(c, now);

    /// <summary>Changes the duration without counting paused time or discarding elapsed growth.</summary>
    public static CountdownSpec ChangeDuration(CountdownSpec c, TimeSpan duration, bool preserveOvergrowth = false)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var delta = duration - c.Duration;
        return c with
        {
            Duration = duration,
            EndsAtUtc = c.EndsAtUtc is { } end ? end + delta : null,
            Remaining = c.Remaining is { } left
                ? (preserveOvergrowth ? left + delta : TimeSpan.FromTicks(Math.Max(0, (left + delta).Ticks)))
                : null,
        };
    }

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

    /// <summary>Farm growth continues past the harvest time and stops displaying at 200%.</summary>
    public static int GrowthPercent(TimeSpan duration, TimeSpan elapsed) =>
        duration <= TimeSpan.Zero ? 0 : (int)Math.Clamp((decimal)elapsed.Ticks * 100 / duration.Ticks, 0, 200);

    public static int? FarmGrowth(CountdownSpec c, DateTimeOffset now) => c switch
    {
        { Status: CountdownStatus.Running, EndsAtUtc: { } end } => GrowthPercent(c.Duration, c.Duration - (end - now)),
        { Status: CountdownStatus.Paused, Remaining: { } left } => GrowthPercent(c.Duration, c.Duration - left),
        _ => null,
    };

    /// <summary>How far along a running or paused countdown is, as <see cref="ProgressPercent"/>; time spent paused
    /// doesn't count. Null while idle.</summary>
    public static int? Progress(CountdownSpec c, DateTimeOffset now) => c switch
    {
        { Status: CountdownStatus.Running, EndsAtUtc: { } end } => ProgressPercent(c.Duration, c.Duration - (end - now)),
        { Status: CountdownStatus.Paused, Remaining: { } left } => ProgressPercent(c.Duration, c.Duration - left),
        _ => null,
    };

    public static CountdownSpec Pause(CountdownSpec c, DateTimeOffset now, bool preserveOvergrowth = false) =>
        c is { Status: CountdownStatus.Running, EndsAtUtc: { } end }
            ? c with
            {
                Status = CountdownStatus.Paused,
                EndsAtUtc = null,
                Remaining = preserveOvergrowth ? end - now : end > now ? end - now : TimeSpan.Zero,
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
