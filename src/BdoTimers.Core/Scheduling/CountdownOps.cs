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

    public static CountdownSpec Reset(CountdownSpec c) =>
        c with { Status = CountdownStatus.Idle, EndsAtUtc = null, Remaining = null, StartedAtUtc = null };

    public static CountdownSpec Complete(CountdownSpec c, DateTimeOffset now) =>
        c.AutoRepeat ? Start(c, now) : Reset(c);
}
