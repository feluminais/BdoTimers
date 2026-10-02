using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

public enum HorseStartResult { Started, LimitReached, Unavailable }

public sealed class TimerStore(JsonFileStore<AppData> file, AppData initial) : PersistentState<AppData>(file, initial)
{
    public const int MaxHorseRegistrations = 10;

    public void SetWeeklyDateRange(Guid id, DateOnly? start, DateOnly? end)
    {
        ScheduleMath.ValidateDateRange(start, end);
        Modify(id, t => t.Scheduled is { } spec ? t with { Scheduled = spec with { StartDate = start, EndDate = end } } : t);
    }

    public void RescheduleEvent(Guid id, DateOnly date, TimeOnly time, string timeZoneId, IClock clock) =>
        Modify(id, t =>
        {
            if (t.OneTime is not { } spec) return t;
            var next = new OneTimeSpec { Date = date, Time = time, TimeZoneId = timeZoneId };
            var at = OneTimeEvents.AtUtc(next);
            if (spec.Date == date && spec.Time == time && spec.TimeZoneId == timeZoneId
                && (!spec.Finished || at <= clock.UtcNow)) return t;
            next = next with { Finished = spec.Finished && at <= clock.UtcNow };
            return t with { OneTime = next };
        });

    /// <summary>Seeds each region once and saves selection and its alert boundary together.</summary>
    public void SelectBossRegion(string regionId, IClock clock)
    {
        BossRegions.Find(regionId);
        Update(data =>
        {
            if (data.SelectedBossRegion == regionId) return data;
            var now = clock.UtcNow;
            var boundary = data.BossAlertsAfterUtc is { } previous && previous > now ? previous : now;
            var seeded = SeedService.ApplyIfNeeded(data, SeedService.LoadEmbedded(regionId), new(), regionId);
            return seeded with
            {
                SelectedBossRegion = regionId, BossSelectionVersion = Guid.NewGuid(), BossAlertsAfterUtc = boundary,
            };
        });
    }

    /// <summary>Starts a separate countdown from the horse preset; active runs retain their own settings and times.</summary>
    public HorseStartResult StartHorseRegistration(DateTimeOffset now)
    {
        var result = HorseStartResult.Unavailable;
        Update(d =>
        {
            var template = d.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration);
            if (template?.Countdown is not { } countdown) return d;
            var runs = d.Timers.Where(t => t.Preset == Presets.HorseRegistrationRun
                && t.Countdown?.Status is not CountdownStatus.Idle).ToList();
            if (runs.Count >= MaxHorseRegistrations)
            {
                result = HorseStartResult.LimitReached;
                return d;
            }
            var used = runs.Select(t => t.HorseRunNumber).ToHashSet();
            var number = Enumerable.Range(1, MaxHorseRegistrations).First(n => !used.Contains(n));
            var run = template with
            {
                Id = Guid.NewGuid(), Name = $"{template.Name} {number}", Preset = Presets.HorseRegistrationRun,
                HorseRunNumber = number, StartHotkey = null, ImageFile = null,
                Countdown = CountdownOps.Start(new CountdownSpec { Duration = countdown.Duration }, now),
            };
            result = HorseStartResult.Started;
            return d with { Timers = [.. d.Timers, run] };
        });
        return result;
    }

    public void Upsert(TimerDef timer)
    {
        Validate(timer);
        Update(d => d with
        {
            Timers = d.Timers.Any(t => t.Id == timer.Id)
                ? d.Timers.Select(t => t.Id == timer.Id ? timer : t).ToList()
                : [.. d.Timers, timer],
            CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Id != timer.Id).ToList(),
        });
    }

    public void Delete(Guid id) => Update(d => d with
    {
        Timers = d.Timers.Where(t => t.Id != id).ToList(),
        Muted = d.Muted.Where(m => m.TimerId != id).ToList(),
        CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Id != id).ToList(),
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
    public void PauseCountdown(Guid id, DateTimeOffset now) => Modify(id, t => t.Countdown is { } c
        ? t with { Countdown = CountdownOps.Pause(c, now, t.Preset == Presets.Farm) } : t);
    public void ResumeCountdown(Guid id, DateTimeOffset now) => ModifyCountdown(id, c => CountdownOps.Resume(c, now));
    public void ResetCountdown(Guid id) => ModifyCountdown(id, CountdownOps.Reset);

    /// <summary>Controls the current state atomically, independently of alert settings.</summary>
    public void ControlCustomCountdown(Guid id, IClock clock) => Update(data =>
    {
        var timer = data.Timers.FirstOrDefault(t => t.Id == id);
        if (timer is null || !CustomCountdowns.Includes(timer)) return data;
        var countdown = timer.Countdown!;
        var now = clock.UtcNow;
        var next = countdown.Status switch
        {
            CountdownStatus.Idle => CountdownOps.Start(countdown, now),
            CountdownStatus.Running => CountdownOps.Pause(countdown, now),
            CountdownStatus.Paused => CountdownOps.Resume(countdown, now),
            _ => countdown,
        };
        return next == countdown ? data : data with
        {
            Timers = data.Timers.Select(t => t.Id == id ? t with { Countdown = next } : t).ToList(),
            CompletedCountdowns = data.CompletedCountdowns.Where(t => t.Id != id).ToList(),
        };
    });

    public void StartStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Start(s, now));
    public void PauseStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Pause(s, now));
    public void ResumeStopwatch(Guid id, DateTimeOffset now) => ModifyStopwatch(id, s => StopwatchOps.Resume(s, now));
    public void ResetStopwatch(Guid id) => ModifyStopwatch(id, StopwatchOps.Reset);

    /// <summary>Runs a countdown or stopwatch as if it had been started at <paramref name="startedAtUtc"/>.</summary>
    public void StartFrom(Guid id, DateTimeOffset startedAtUtc) => Modify(id, t => t with
    {
        Countdown = t.Countdown is { } c ? CountdownOps.StartFrom(c, startedAtUtc) : null,
        Stopwatch = t.Stopwatch is { } s ? StopwatchOps.StartFrom(s, startedAtUtc) : null,
    });

    /// <summary>Completes running countdowns that ended at or before <paramref name="endedBefore"/>; returns them as they were before completion.</summary>
    public IReadOnlyList<TimerDef> CompleteCountdowns(DateTimeOffset now, DateTimeOffset endedBefore)
    {
        List<TimerDef> completed = [];
        Update(d =>
        {
            var due = d.Timers
                .Where(t => t.Preset != Presets.Farm
                    && t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } && end <= endedBefore)
                .ToList();
            if (due.Count == 0) return d;
            completed.AddRange(due);
            var ids = due.Select(t => t.Id).ToHashSet();
            var removedIds = due.Where(t => t.Preset == Presets.HorseRegistrationRun).Select(t => t.Id).ToHashSet();
            return d with
            {
                Timers = d.Timers
                    .Where(t => !ids.Contains(t.Id) || t.Preset != Presets.HorseRegistrationRun)
                    .Select(t => ids.Contains(t.Id) ? t with { Countdown = CountdownOps.Complete(t.Countdown!) } : t)
                    .ToList(),
                Muted = removedIds.Count == 0 ? d.Muted : d.Muted.Where(m => !removedIds.Contains(m.TimerId)).ToList(),
                CompletedCountdowns = [.. d.CompletedCountdowns.Where(t => !ids.Contains(t.Id)
                    && t.Countdown!.EndsAtUtc >= now - AlertPlanner.Memory), .. due],
            };
        });
        return completed;
    }

    public void PruneMuted(DateTimeOffset before) => Update(d =>
        d.Muted.Any(m => m.OccurrenceUtc < before) || d.CompletedCountdowns.Any(t => t.Countdown!.EndsAtUtc < before)
            ? d with
            {
                Muted = d.Muted.Where(m => m.OccurrenceUtc >= before).ToList(),
                CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Countdown!.EndsAtUtc >= before).ToList(),
            }
            : d);

    /// <summary>Persists completion before returning events for a notice; startup never replays a past event.</summary>
    public IReadOnlyList<TimerDef> CompleteEvents(IClock clock, bool startup = false)
    {
        var now = clock.UtcNow;
        List<TimerDef> completed = [];
        Update(data =>
        {
            var due = data.Timers.Where(t => t.Kind == TimerKind.OneTime && t.OneTime is { Finished: false })
                .Where(t => startup ? OneTimeEvents.AtUtc(t.OneTime!) < now : OneTimeEvents.AtUtc(t.OneTime!) <= now)
                .ToList();
            if (due.Count == 0) return data;
            var ids = due.Select(t => t.Id).ToHashSet();
            completed.AddRange(due);
            return data with
            {
                Timers = data.Timers.Select(t => ids.Contains(t.Id) ? t with { OneTime = t.OneTime! with { Finished = true } } : t).ToList(),
            };
        });
        return completed;
    }

    /// <summary>The selected region's bosses return to alerts on with default settings; spawn times and custom timers stay.</summary>
    public void ResetBossAlerts() => Update(d => d with
    {
        Timers = d.Timers.Select(t => BossRegions.IsSelected(d, t) ? t with { Enabled = true, Alerts = new AlertConfig() } : t).ToList(),
    });

    public void Modify(Guid id, Func<TimerDef, TimerDef> change) => Update(d =>
    {
        var previous = d.Timers.FirstOrDefault(t => t.Id == id);
        if (previous is null) return d;
        var next = Validated(change(previous));
        return d with
        {
            Timers = d.Timers.Select(t => t.Id == id ? next : t).ToList(),
            CompletedCountdowns = ReferenceEquals(previous.Countdown, next.Countdown) ? d.CompletedCountdowns
                : d.CompletedCountdowns.Where(t => t.Id != id).ToList(),
        };
    });

    static TimerDef Validated(TimerDef timer) { Validate(timer); return timer; }

    static void Validate(TimerDef timer)
    {
        if (timer.Scheduled is { } spec) ScheduleMath.ValidateDateRange(spec.StartDate, spec.EndDate);
    }

    void ModifyCountdown(Guid id, Func<CountdownSpec, CountdownSpec> change) =>
        Modify(id, t => t.Countdown is null ? t : t with { Countdown = change(t.Countdown) });

    void ModifyStopwatch(Guid id, Func<StopwatchSpec, StopwatchSpec> change) =>
        Modify(id, t => t.Stopwatch is null ? t : t with { Stopwatch = change(t.Stopwatch) });
}
