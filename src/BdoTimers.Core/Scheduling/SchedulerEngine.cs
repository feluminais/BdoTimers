using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Scheduling;

public sealed class SchedulerEngine(
    TimerStore timers, PersistentState<AppSettings> settings, IAlertSink sink, IClock clock)
{
    static readonly TimeSpan MuteRetention = TimeSpan.FromHours(1);
    /// <summary>How early the voice gets ready for a spoken alert; loading it takes about a second.</summary>
    static readonly TimeSpan SpeechLead = TimeSpan.FromMinutes(1);

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
        var muted = data.Muted.ToHashSet();
        var defaultLeads = settings.Current.DefaultLeadTimesMinutes;
        var alerts = AlertGrouping.Group(_planner.Tick(data.Timers, muted, now, defaultLeads));
        // The planner runs even while paused so that resuming doesn't replay the paused period.
        if (!AlertPause.IsPaused(settings.Current, now))
        {
            foreach (var alert in alerts) sink.Dispatch(alert);
            if (AlertPlanner.SpeechDueWithin(data.Timers, muted, now, SpeechLead, defaultLeads)) sink.PrepareSpeech();
        }

        timers.CompleteCountdowns(now, now);
        timers.PruneMuted(now - MuteRetention);
    }
}
