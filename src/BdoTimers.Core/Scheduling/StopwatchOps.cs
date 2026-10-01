using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class StopwatchOps
{
    /// <summary>Counts from <paramref name="startedAtUtc"/>: now, or earlier for a stopwatch started late.</summary>
    public static StopwatchSpec Start(StopwatchSpec s, DateTimeOffset startedAtUtc) =>
        s with { Status = CountdownStatus.Running, StartedAtUtc = startedAtUtc, Elapsed = null };

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

    public static TimeSpan Elapsed(StopwatchSpec s, DateTimeOffset now) => s switch
    {
        { Status: CountdownStatus.Running, StartedAtUtc: { } started } => Positive(now - started),
        { Status: CountdownStatus.Paused, Elapsed: { } counted } => counted,
        _ => TimeSpan.Zero,
    };

    static TimeSpan Positive(TimeSpan span) => span > TimeSpan.Zero ? span : TimeSpan.Zero;
}
