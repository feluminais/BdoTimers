using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class CountdownOps
{
    public static CountdownSpec Start(CountdownSpec c, DateTimeOffset now) =>
        c with { Status = CountdownStatus.Running, EndsAtUtc = now + c.Duration, Remaining = null, StartedAtUtc = now };

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

    /// <summary>Corrects the time left of a started countdown, such as one started late; the start time moves to match.
    /// An idle countdown is unchanged.</summary>
    public static CountdownSpec SetRemaining(CountdownSpec c, DateTimeOffset now, TimeSpan left) => c.Status switch
    {
        CountdownStatus.Running => c with
        {
            EndsAtUtc = now + left,
            StartedAtUtc = left < c.Duration ? now - (c.Duration - left) : now,
        },
        CountdownStatus.Paused => c with { Remaining = left },
        _ => c,
    };

    public static CountdownSpec Reset(CountdownSpec c) =>
        c with { Status = CountdownStatus.Idle, EndsAtUtc = null, Remaining = null, StartedAtUtc = null };

    /// <summary>A finished countdown goes back to Ready.</summary>
    public static CountdownSpec Complete(CountdownSpec c) => Reset(c);
}
