using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Scheduling;

public sealed class SchedulerEngine(
    TimerStore timers, PersistentState<AppSettings> settings, IAlertSink sink, IClock clock)
{
    static readonly TimeSpan MuteRetention = TimeSpan.FromHours(1);

    readonly AlertPlanner _planner = new();

    /// <summary>Run once before the first tick: completes countdowns that ended while the app was closed.</summary>
    public void ReconcileStartup()
    {
        var now = clock.UtcNow;
        foreach (var timer in timers.CompleteCountdowns(now, now - AlertPlanner.Grace))
            sink.NotifyEndedWhileAway(timer);
    }

    public void Tick()
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var alerts = _planner.Tick(data.Timers, data.Muted.ToHashSet(), now);
        // The planner runs even while paused so that resuming doesn't replay the paused period.
        if (!AlertPause.IsPaused(settings.Current, now))
            foreach (var alert in alerts) sink.Dispatch(alert);

        timers.CompleteCountdowns(now, now);
        timers.PruneMuted(now - MuteRetention);
    }
}
