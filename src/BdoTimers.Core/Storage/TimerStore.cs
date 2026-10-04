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
            // War of the Roses follows the region unless the player changed it.
            var timers = seeded.Timers.Select(t => t.Preset == Presets.WarOfTheRoses
                && Presets.IsDefaultWarOfTheRoses(t.Scheduled, data.SelectedBossRegion)
                    ? t with { Scheduled = Presets.WarOfTheRosesSchedule(regionId) } : t).ToList();
            return seeded with
            {
                Timers = timers, SelectedBossRegion = regionId, BossSelectionVersion = Guid.NewGuid(), BossAlertsAfterUtc = boundary,
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

    /// <summary>Puts War of the Roses back to the selected region's times; its alert settings stay.</summary>
    public void ResetWarOfTheRoses() => Update(d => d with
    {
        Timers = d.Timers.Select(t => t.Preset == Presets.WarOfTheRoses
            ? t with { Scheduled = Presets.WarOfTheRosesSchedule(d.SelectedBossRegion) } : t).ToList(),
    });

    /// <summary>Adds a boss to the selected region and returns it.</summary>
    public TimerDef AddBoss()
    {
        TimerDef? added = null;
        Update(d =>
        {
            added = BossEdits.Create(d);
            return d with { Timers = [.. d.Timers, added] };
        });
        return added!;
    }

    /// <summary>Renames a boss the player added; false when the name is blank or another boss of its region has it.</summary>
    public bool RenameBoss(Guid id, string name)
    {
        var trimmed = name.Trim();
        var renamed = false;
        Update(d =>
        {
            var boss = d.Timers.FirstOrDefault(t => t.Id == id && t.AddedByUser);
            if (boss is null || trimmed.Length == 0 || !BossEdits.IsNameFree(d, BossRegions.RegionOf(boss), trimmed, id)) return d;
            renamed = true;
            return boss.Name == trimmed ? d : d with
            {
                Timers = d.Timers.Select(t => t.Id == id ? t with { Name = trimmed } : t).ToList(),
            };
        });
        return renamed;
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

    public void Delete(Guid id) => Update(d =>
        !d.Timers.Any(t => t.Id == id) && !d.Muted.Any(m => m.TimerId == id) && !d.CompletedCountdowns.Any(t => t.Id == id)
            ? d : d with
            {
                Timers = d.Timers.Where(t => t.Id != id).ToList(),
                Muted = d.Muted.Where(m => m.TimerId != id).ToList(),
                CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Id != id).ToList(),
            });

    internal DeletedTimer? DeleteForUndo(Guid id)
    {
        DeletedTimer? deleted = null;
        Update(d =>
        {
            var index = d.Timers.ToList().FindIndex(t => t.Id == id);
            if (index < 0) return d;
            deleted = new(d.Timers[index], index, d.Muted.Where(m => m.TimerId == id).ToList(),
                d.CompletedCountdowns.Where(t => t.Id == id).ToList());
            return d with
            {
                Timers = d.Timers.Where(t => t.Id != id).ToList(),
                Muted = d.Muted.Where(m => m.TimerId != id).ToList(),
                CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Id != id).ToList(),
            };
        });
        return deleted;
    }

    internal bool RestoreDeleted(DeletedTimer deleted)
    {
        var restored = false;
        Update(d =>
        {
            var timer = deleted.Timer;
            if (d.Timers.Any(t => t.Id == timer.Id)) return d;
            // A reset or timetable update since may have brought back a boss of the same name.
            if (timer.IsBuiltIn && !BossEdits.IsNameFree(d, BossRegions.RegionOf(timer), timer.Name)) return d;
            if (Presets.IsActiveHorseRun(timer) && (d.Timers.Count(Presets.IsActiveHorseRun) >= MaxHorseRegistrations
                || d.Timers.Any(t => Presets.IsActiveHorseRun(t) && t.HorseRunNumber == timer.HorseRunNumber))) return d;
            var timers = d.Timers.ToList();
            timers.Insert(Math.Min(deleted.Index, timers.Count), timer);
            restored = true;
            return d with
            {
                Timers = timers,
                Muted = [.. d.Muted.Where(m => m.TimerId != timer.Id), .. deleted.Muted],
                CompletedCountdowns = [.. d.CompletedCountdowns.Where(t => t.Id != timer.Id), .. deleted.Completed],
            };
        });
        return restored;
    }

    internal ResetTimer? ResetForUndo(Guid id)
    {
        ResetTimer? reset = null;
        Update(d =>
        {
            var timer = d.Timers.FirstOrDefault(t => t.Id == id);
            if (timer is null) return d;
            var next = ResetRun(timer);
            if (next == timer && !d.CompletedCountdowns.Any(t => t.Id == id)) return d;
            reset = new(timer, next, d.CompletedCountdowns.Where(t => t.Id == id).ToList());
            return d with
            {
                Timers = d.Timers.Select(t => t.Id == id ? next : t).ToList(),
                CompletedCountdowns = d.CompletedCountdowns.Where(t => t.Id != id).ToList(),
            };
        });
        return reset;
    }

    internal bool RestoreReset(ResetTimer reset)
    {
        var restored = false;
        Update(d =>
        {
            var current = d.Timers.FirstOrDefault(t => t.Id == reset.Before.Id);
            if (current is null || current.Kind != reset.After.Kind || current.Preset != reset.After.Preset
                || current.Countdown != reset.After.Countdown || current.Stopwatch != reset.After.Stopwatch) return d;
            restored = true;
            var next = current with { Countdown = reset.Before.Countdown, Stopwatch = reset.Before.Stopwatch };
            return d with
            {
                Timers = d.Timers.Select(t => t.Id == current.Id ? next : t).ToList(),
                CompletedCountdowns = [.. d.CompletedCountdowns.Where(t => t.Id != current.Id), .. reset.Completed],
            };
        });
        return restored;
    }

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
    public void Reset(Guid id) => Modify(id, ResetRun);

    static TimerDef ResetRun(TimerDef timer) => timer with
    {
        Countdown = timer.Countdown is { } c ? CountdownOps.Reset(c) : null,
        Stopwatch = timer.Stopwatch is { } s ? StopwatchOps.Reset(s) : null,
    };

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

    /// <summary>Completes running countdowns that ended at or before <paramref name="endedBefore"/>; returns them as they were before completion.</summary>
    public IReadOnlyList<TimerDef> CompleteCountdowns(DateTimeOffset now, DateTimeOffset endedBefore)
    {
        List<TimerDef> completed = [];
        Update(d =>
        {
            var due = d.Timers
                .Where(t => !Presets.Overgrows(t.Preset)
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
                    .Select(t => ids.Contains(t.Id) ? t with { Countdown = CountdownOps.Reset(t.Countdown!) } : t)
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
        if (ReferenceEquals(next, previous)) return d;
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
        if (timer.Scheduled is not { } spec) return;
        ScheduleMath.ValidateDateRange(spec.StartDate, spec.EndDate);
        ScheduleMath.ValidateEveryWeeks(spec.EveryWeeks);
    }

    /// <summary>Changes whichever of a countdown and a stopwatch the timer has.</summary>
    void ModifyRun(Guid id, Func<TimerDef, CountdownSpec, CountdownSpec> countdown, Func<StopwatchSpec, StopwatchSpec> stopwatch) =>
        Modify(id, t => t with
        {
            Countdown = t.Countdown is { } c ? countdown(t, c) : null,
            Stopwatch = t.Stopwatch is { } s ? stopwatch(s) : null,
        });
}
