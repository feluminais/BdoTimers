using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

public sealed class SchedulerEngine(
    TimerStore timers, PersistentState<AppSettings> settings, IAlertSink sink, IClock clock)
{
    /// <summary>How early speech is prepared before an occurrence's next spoken alert; loading the voice and saying a line
    /// take a few seconds at low priority.</summary>
    static readonly TimeSpan SpeechLead = TimeSpan.FromMinutes(1);

    readonly AlertPlanner _planner = new();

    /// <summary>Run before the first tick: completes countdowns and events missed while the app was closed.</summary>
    public void ReconcileStartup()
    {
        var now = clock.UtcNow;
        foreach (var timer in timers.CompleteCountdowns(now, now - AlertPlanner.Grace))
            sink.NotifyEndedWhileAway(timer);
        foreach (var timer in timers.CompleteEvents(clock, startup: true))
            sink.NotifyEndedWhileAway(timer);
    }

    public void Tick()
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var muted = GarmothTracker.Silenced(data);
        var defaultLeads = settings.Current.DefaultLeadTimesMinutes;
        var eligible = data.Timers.Where(t => BossRegions.IsEligible(data, t)).ToList();
        var alerts = AlertGrouping.Group(_planner.Tick(eligible, muted, now, defaultLeads, data.BossAlertsAfterUtc));
        // The planner runs even while paused so that resuming doesn't replay the paused period.
        var paused = AlertPause.IsPaused(settings.Current, now);
        if (!paused)
        {
            foreach (var alert in alerts)
                if (AlertEligibility.Filter(timers.Current, alert with { BossSelectionVersion = data.BossSelectionVersion }) is { } current)
                    sink.Dispatch(current);
            var speech = _planner.UpcomingSpeech(eligible, muted, now, SpeechLead, defaultLeads, data.BossAlertsAfterUtc);
            if (speech.Count > 0 && timers.Current.BossSelectionVersion == data.BossSelectionVersion) sink.PrepareSpeech(speech);
        }

        // An end older than the planner's grace was never alerted: the PC slept through it or the loop stalled.
        foreach (var timer in timers.CompleteCountdowns(now, now))
            if (!paused && timer.Countdown!.EndsAtUtc < now - AlertPlanner.Grace) sink.NotifyEndedWhileAway(timer);
        foreach (var timer in timers.CompleteEvents(clock))
            if (!paused && OneTimeEvents.AtUtc(timer.OneTime!) < now - AlertPlanner.Grace) sink.NotifyEndedWhileAway(timer);
        // Skips are kept as long as fired alerts, so setting the clock back doesn't replay either.
        timers.PruneMuted(now - AlertPlanner.Memory);
    }
}
