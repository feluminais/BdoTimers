using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Storage;

public sealed class TimerStore(JsonFileStore<AppData> file, AppData initial) : PersistentState<AppData>(file, initial)
{
    public void Upsert(TimerDef timer) => Update(d => d with
    {
        Timers = d.Timers.Any(t => t.Id == timer.Id)
            ? d.Timers.Select(t => t.Id == timer.Id ? timer : t).ToList()
            : [.. d.Timers, timer],
    });

    public void Delete(Guid id) => Update(d => d with
    {
        Timers = d.Timers.Where(t => t.Id != id).ToList(),
        Muted = d.Muted.Where(m => m.TimerId != id).ToList(),
    });

    public void SetEnabled(Guid id, bool enabled) => Modify(id, t => t with { Enabled = enabled });

    public void ToggleMute(Guid id, DateTimeOffset occurrenceUtc) => Update(d =>
    {
        var mute = new MutedOccurrence(id, occurrenceUtc);
        return d with { Muted = d.Muted.Contains(mute) ? d.Muted.Where(m => m != mute).ToList() : [.. d.Muted, mute] };
    });

    /// <summary>Timers that played <paramref name="key"/> go back to the app-wide sound.</summary>
    public void ForgetSound(string key) => Update(d => d with
    {
        Timers = d.Timers
            .Select(t => t.Alerts.Sound.Key == key ? t with { Alerts = t.Alerts with { Sound = t.Alerts.Sound with { Key = null } } } : t)
            .ToList(),
    });

    public void StartCountdown(Guid id, DateTimeOffset now) => ModifyCountdown(id, c => CountdownOps.Start(c, now));
    public void PauseCountdown(Guid id, DateTimeOffset now) => ModifyCountdown(id, c => CountdownOps.Pause(c, now));
    public void ResumeCountdown(Guid id, DateTimeOffset now) => ModifyCountdown(id, c => CountdownOps.Resume(c, now));
    public void ResetCountdown(Guid id) => ModifyCountdown(id, CountdownOps.Reset);
    public void SetTimeLeft(Guid id, DateTimeOffset now, TimeSpan left) => ModifyCountdown(id, c => CountdownOps.SetRemaining(c, now, left));

    public void StartStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Start(s, now));
    public void PauseStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Pause(s, now));
    public void ResumeStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Resume(s, now));
    public void ResetStopwatch(Guid id) => ModifyStopwatch(id, StopwatchOps.Reset);
    public void SetElapsed(Guid id, DateTimeOffset now, TimeSpan elapsed) => ModifyStopwatch(id, s => StopwatchOps.SetElapsed(s, now, elapsed));

    /// <summary>Completes running countdowns that ended at or before <paramref name="endedBefore"/>; returns them as they were before completion.</summary>
    public IReadOnlyList<TimerDef> CompleteCountdowns(DateTimeOffset now, DateTimeOffset endedBefore)
    {
        List<TimerDef> completed = [];
        Update(d =>
        {
            var due = d.Timers
                .Where(t => t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } && end <= endedBefore)
                .ToList();
            if (due.Count == 0) return d;
            completed.AddRange(due);
            var ids = due.Select(t => t.Id).ToHashSet();
            return d with
            {
                Timers = d.Timers
                    .Select(t => ids.Contains(t.Id) ? t with { Countdown = CountdownOps.Complete(t.Countdown!, now) } : t)
                    .ToList(),
            };
        });
        return completed;
    }

    public void PruneMuted(DateTimeOffset before) => Update(d =>
        d.Muted.Any(m => m.OccurrenceUtc < before)
            ? d with { Muted = d.Muted.Where(m => m.OccurrenceUtc >= before).ToList() }
            : d);

    /// <summary>Every built-in boss back to alerts on with the default alert settings; spawn times and custom timers stay.</summary>
    public void ResetBossAlerts() => Update(d => d with
    {
        Timers = d.Timers.Select(t => t.IsBuiltIn ? t with { Enabled = true, Alerts = new AlertConfig() } : t).ToList(),
    });

    public void Modify(Guid id, Func<TimerDef, TimerDef> change) =>
        Update(d => d with { Timers = d.Timers.Select(t => t.Id == id ? change(t) : t).ToList() });

    void ModifyCountdown(Guid id, Func<CountdownSpec, CountdownSpec> change) =>
        Modify(id, t => t.Countdown is null ? t : t with { Countdown = change(t.Countdown) });

    void ModifyStopwatch(Guid id, Func<StopwatchSpec, StopwatchSpec> change) =>
        Modify(id, t => t.Stopwatch is null ? t : t with { Stopwatch = change(t.Stopwatch) });
}
