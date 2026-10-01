using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Diagnostics;

/// <summary>
/// Logs the failures of an operation that is retried every few seconds without flooding the log: the first failure
/// with its stack trace, identical repeats as a one-line reminder at most once an hour, and the recovery once.
/// For use from one thread.
/// </summary>
public sealed class RepeatingErrorLog(string operation, IClock clock)
{
    static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(1);

    string? _failure;
    DateTimeOffset _loggedAt;
    int _repeats;

    public void Failed(Exception ex)
    {
        var failure = $"{ex.GetType().FullName}: {ex.Message}";
        var now = clock.UtcNow;
        if (failure != _failure)
        {
            Log.Error($"{operation} failed", ex);
            (_failure, _loggedAt, _repeats) = (failure, now, 0);
            return;
        }
        _repeats++;
        // Duration, so a clock set back doesn't silence the reminders until it catches up.
        if ((now - _loggedAt).Duration() < ReminderInterval) return;
        Log.Error($"{operation} still failing ({_repeats} times): {ex.Message}");
        (_loggedAt, _repeats) = (now, 0);
    }

    public void Succeeded()
    {
        if (_failure is null) return;
        Log.Info($"{operation} recovered");
        _failure = null;
    }
}
