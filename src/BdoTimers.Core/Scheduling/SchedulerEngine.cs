using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Scheduling;

public sealed class SchedulerEngine(
    TimerStore timers, PersistentState<AppSettings> settings, IAlertSink sink, IClock clock)
{
    /// <summary>How early speech is prepared before an occurrence's next spoken alert; loading the voice and saying a line
    /// take a few seconds at low priority.</summary>
    static readonly TimeSpan SpeechLead = TimeSpan.FromMinutes(1);

    readonly AlertPlanner _planner = new();

    /// <summary>Run once before the first tick: completes countdowns that ended while the app was closed.</summary>
    public void ReconcileStartup()
    {
        var now = clock.UtcNow;
        foreach (var timer in timers.CompleteCountdowns(now - AlertPlanner.Grace))
            sink.NotifyEndedWhileAway(timer);
    }

    public void Tick()
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var muted = data.Muted.ToHashSet();
        var defaultLeads = settings.Current.DefaultLeadTimesMinutes;
        var alerts = AlertGrouping.Group(_planner.Tick(data.Timers, muted, now, defaultLeads));
        // The planner runs even while paused so that resuming doesn't replay the paused period.
        var paused = AlertPause.IsPaused(settings.Current, now);
        if (!paused)
        {
            foreach (var alert in alerts) sink.Dispatch(alert);
            var speech = _planner.UpcomingSpeech(data.Timers, muted, now, SpeechLead, defaultLeads);
            if (speech.Count > 0) sink.PrepareSpeech(speech);
        }

        // An end older than the planner's grace was never alerted: the PC slept through it or the loop stalled.
        foreach (var timer in timers.CompleteCountdowns(now))
            if (!paused && timer.Countdown!.EndsAtUtc < now - AlertPlanner.Grace) sink.NotifyEndedWhileAway(timer);
        // Skips are kept as long as fired alerts, so setting the clock back doesn't replay either.
        timers.PruneMuted(now - AlertPlanner.Memory);
    }
}
