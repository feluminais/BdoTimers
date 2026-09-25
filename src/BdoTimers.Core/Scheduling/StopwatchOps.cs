using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class StopwatchOps
{
    public static StopwatchSpec Start(StopwatchSpec s, DateTimeOffset now) =>
        s with { Status = CountdownStatus.Running, StartedAtUtc = now, Elapsed = null };

    public static StopwatchSpec Pause(StopwatchSpec s, DateTimeOffset now) =>
        s is { Status: CountdownStatus.Running, StartedAtUtc: { } started }
            ? s with { Status = CountdownStatus.Paused, StartedAtUtc = null, Elapsed = Positive(now - started) }
            : s;

    public static StopwatchSpec Resume(StopwatchSpec s, DateTimeOffset now) =>
        s is { Status: CountdownStatus.Paused, Elapsed: { } counted }
            ? s with { Status = CountdownStatus.Running, StartedAtUtc = now - counted, Elapsed = null }
            : s;

    public static StopwatchSpec Reset(StopwatchSpec s) =>
        s with { Status = CountdownStatus.Idle, StartedAtUtc = null, Elapsed = null };

    /// <summary>Corrects the time counted, such as for one started late; an idle stopwatch starts from there.</summary>
    public static StopwatchSpec SetElapsed(StopwatchSpec s, DateTimeOffset now, TimeSpan elapsed) =>
        s.Status == CountdownStatus.Paused
            ? s with { Elapsed = elapsed }
            : s with { Status = CountdownStatus.Running, StartedAtUtc = now - elapsed, Elapsed = null };

    public static TimeSpan Elapsed(StopwatchSpec s, DateTimeOffset now) => s switch
    {
        { Status: CountdownStatus.Running, StartedAtUtc: { } started } => Positive(now - started),
        { Status: CountdownStatus.Paused, Elapsed: { } counted } => counted,
        _ => TimeSpan.Zero,
    };

    static TimeSpan Positive(TimeSpan span) => span > TimeSpan.Zero ? span : TimeSpan.Zero;
}
