using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class AlertPause
{
    /// <summary>A null <paramref name="duration"/> pauses until resumed.</summary>
    public static AppSettings Pause(AppSettings s, DateTimeOffset now, TimeSpan? duration) =>
        s with { AlertsPausedUntilUtc = duration is { } d ? now + d : DateTimeOffset.MaxValue };

    public static AppSettings Resume(AppSettings s) => s with { AlertsPausedUntilUtc = null };

    public static bool IsPaused(AppSettings s, DateTimeOffset now) =>
        s.AlertsPausedUntilUtc is { } until && now < until;

    /// <summary>Time left on a timed pause; null when not paused or paused until resumed.</summary>
    public static TimeSpan? Remaining(AppSettings s, DateTimeOffset now) =>
        IsPaused(s, now) && s.AlertsPausedUntilUtc != DateTimeOffset.MaxValue ? s.AlertsPausedUntilUtc - now : null;
}
