using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

public enum HorseStartResult { Started, LimitReached, Unavailable }

public sealed class TimerStore(JsonFileStore<AppData> file, AppData initial) : PersistentState<AppData>(file, initial)
{
    public const int MaxHorseRegistrations = 10;

    /// <summary>Starts a separate countdown from the horse preset; active runs retain their own settings and times.</summary>
    public HorseStartResult StartHorseRegistration(DateTimeOffset now)
    {
        var result = HorseStartResult.Unavailable;
        Update(d =>
        {
            var template = d.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration);
            if (template?.Countdown is not { } countdown) return d;
            var runs = d.Timers.Where(Presets.IsActiveHorseRun).ToList();
            if (runs.Count >= MaxHorseRegistrations)
            {
                result = HorseStartResult.LimitReached;
                return d;
            }
            var used = runs.Select(t => t.HorseRunNumber).ToHashSet();
            var number = Enumerable.Range(1, MaxHorseRegistrations).First(n => !used.Contains(n));
            var run = Presets.HorseRun(template, number) with
            {
                Countdown = CountdownOps.Start(new CountdownSpec { Duration = countdown.Duration }, now),
            };
            result = HorseStartResult.Started;
            return d with { Timers = [.. d.Timers, run] };
        });
        return result;
    }

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

    /// <summary>Runs a countdown or stopwatch from <paramref name="startedAtUtc"/>: now, or earlier when started late.</summary>
    public void Start(Guid id, DateTimeOffset startedAtUtc) =>
        ModifyRun(id, (_, c) => CountdownOps.Start(c, startedAtUtc), s => StopwatchOps.Start(s, startedAtUtc));
    public void Pause(Guid id, DateTimeOffset now) =>
        ModifyRun(id, (t, c) => CountdownOps.Pause(c, now, Presets.Overgrows(t.Preset)), s => StopwatchOps.Pause(s, now));
    public void Resume(Guid id, DateTimeOffset now) =>
        ModifyRun(id, (_, c) => CountdownOps.Resume(c, now), s => StopwatchOps.Resume(s, now));
    public void Reset(Guid id) => ModifyRun(id, (_, c) => CountdownOps.Reset(c), StopwatchOps.Reset);

    /// <summary>
    /// Completes running countdowns that ended at or before <paramref name="endedBefore"/>, removing finished horse
    /// registrations with their skips; returns the countdowns as they were before completion.
    /// </summary>
    public IReadOnlyList<TimerDef> CompleteCountdowns(DateTimeOffset endedBefore)
    {
        List<TimerDef> completed = [];
        Update(d =>
        {
            completed.AddRange(d.Timers.Where(t => !Presets.Overgrows(t.Preset)
                && t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } && end <= endedBefore));
            if (completed.Count == 0) return d;
            var ids = completed.Select(t => t.Id).ToHashSet();
            var removed = completed.Where(t => t.Preset == Presets.HorseRegistrationRun).Select(t => t.Id).ToHashSet();
            return d with
            {
                Timers = d.Timers
                    .Where(t => !removed.Contains(t.Id))
                    .Select(t => ids.Contains(t.Id) ? t with { Countdown = CountdownOps.Reset(t.Countdown!) } : t)
                    .ToList(),
                Muted = d.Muted.Where(m => !removed.Contains(m.TimerId)).ToList(),
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

    /// <summary>Changes whichever of a countdown and a stopwatch the timer has.</summary>
    void ModifyRun(Guid id, Func<TimerDef, CountdownSpec, CountdownSpec> countdown, Func<StopwatchSpec, StopwatchSpec> stopwatch) =>
        Modify(id, t => t with
        {
            Countdown = t.Countdown is { } c ? countdown(t, c) : null,
            Stopwatch = t.Stopwatch is { } s ? stopwatch(s) : null,
        });
}
