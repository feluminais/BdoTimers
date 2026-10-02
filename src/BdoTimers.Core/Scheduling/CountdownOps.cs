using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class CountdownOps
{
    /// <summary>Runs from <paramref name="startedAtUtc"/>: now, or earlier for a countdown started late.</summary>
    public static CountdownSpec Start(CountdownSpec c, DateTimeOffset startedAtUtc) =>
        c with { Status = CountdownStatus.Running, EndsAtUtc = startedAtUtc + c.Duration, Remaining = null, StartedAtUtc = startedAtUtc };

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

    /// <summary>When a countdown <paramref name="percent"/> % of the way through <paramref name="duration"/> at
    /// <paramref name="now"/> started, e.g. a crop showing that much growth.</summary>
    public static DateTimeOffset StartForProgress(TimeSpan duration, int percent, DateTimeOffset now) =>
        now - TimeSpan.FromTicks(duration.Ticks * percent / 100);

    /// <summary>The whole percent of <paramref name="duration"/> that <paramref name="elapsed"/> covers, rounded down
    /// like the game's growth display and kept within 0-100; a countdown that <paramref name="overgrows"/> past its end
    /// (<see cref="Seed.Presets.Overgrows"/>) goes on to 200.</summary>
    public static int ProgressPercent(TimeSpan duration, TimeSpan elapsed, bool overgrows = false) =>
        duration <= TimeSpan.Zero ? 0 : (int)Math.Clamp((decimal)elapsed.Ticks * 100 / duration.Ticks, 0, overgrows ? 200 : 100);

    /// <summary>The time left of a running or paused countdown, negative once it is over; null while idle.</summary>
    public static TimeSpan? Left(CountdownSpec c, DateTimeOffset now) => c switch
    {
        { Status: CountdownStatus.Running, EndsAtUtc: { } end } => end - now,
        { Status: CountdownStatus.Paused, Remaining: { } left } => left,
        _ => null,
    };

    /// <summary>How far along a running or paused countdown is, as <see cref="ProgressPercent"/>; time spent paused
    /// doesn't count. Null while idle.</summary>
    public static int? Progress(CountdownSpec c, DateTimeOffset now, bool overgrows = false) =>
        Left(c, now) is { } left ? ProgressPercent(c.Duration, c.Duration - left, overgrows) : null;

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

    /// <summary>Back to Ready; also where a finished countdown goes.</summary>
    public static CountdownSpec Reset(CountdownSpec c) =>
        c with { Status = CountdownStatus.Idle, EndsAtUtc = null, Remaining = null, StartedAtUtc = null };
}
