using System.Diagnostics.CodeAnalysis;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

/// <summary>Validates saved models for normal loading and backup restore, including legacy data before migration.</summary>
internal static class SavedDataValidation
{
    public static void Check(object value)
    {
        try
        {
            Require(value is not null);
            switch (value)
            {
                case BackupSnapshot snapshot:
                    Check(snapshot.Settings);
                    Check(snapshot.Timers);
                    Check(snapshot.Todos);
                    break;
                case AppSettings settings: Settings(settings); break;
                case AppData timers: Timers(timers); break;
                case TodoData todos: Todos(todos); break;
            }
        }
        catch (Exception ex) when (ex is NullReferenceException or ArgumentException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidDataException("The saved data is invalid.", ex);
        }
    }

    static void Settings(AppSettings settings)
    {
        Require(double.IsFinite(settings.TextScale) && settings.TextScale is >= 1 and <= 1.5);
        Require(settings.DefaultLeadTimesMinutes is not null && settings.DefaultLeadTimesMinutes.All(m => m is >= 0 and <= 1440));
        Require(float.IsFinite(settings.Volume) && settings.Volume is >= 0 and <= 1 && settings.AlertSound is not null);
        Require(settings.Overlay is not null && Enum.IsDefined(settings.Overlay.Layout));
        Require(Enum.IsDefined(settings.Overlay.MouseProximity));
        Require(double.IsFinite(settings.Overlay.Scale) && settings.Overlay.Scale is >= 0.6 and <= 2);
        Require(double.IsFinite(settings.Overlay.BackgroundOpacity) && settings.Overlay.BackgroundOpacity is >= 0 and <= 1);
        Require(double.IsFinite(settings.Overlay.TextOpacity) && settings.Overlay.TextOpacity is >= 0.2 and <= 1);
        Require(settings.Overlay.BackgroundColor is not null && settings.Overlay.GuildBosses is not null);
        Require(settings.Overlay.ShowSeconds > 0);
        Require(settings.Overlay.Timers is not null && settings.Overlay.Timers.All(t => t is { Minutes: > 0 and <= 10080 }));
        Require(new[] { settings.Overlay.NextLead, settings.Overlay.FarmLead, settings.Overlay.HorseLead, settings.Overlay.CustomLead }
            .All(l => l is { Minutes: > 0 and <= 10080 }));
        Require(settings.OverlayLeft is not { } left || double.IsFinite(left));
        Require(settings.OverlayTop is not { } top || double.IsFinite(top));
        if (settings.Window is { } window)
            Require(double.IsFinite(window.Left) && double.IsFinite(window.Top) && double.IsFinite(window.Width)
                && double.IsFinite(window.Height) && window.Width > 0 && window.Height > 0);
        FileName(settings.Overlay.BackgroundImage);
        Schedule(settings.DailyTodoReset);
        Schedule(settings.WeeklyTodoReset);
        Require(settings.Calendar is not null);
    }

    static void Timers(AppData timers)
    {
        Require(timers.DataVersion is >= 0 and <= DataMigrations.Current);
        Require(timers.Timers is not null && timers.Muted is not null);
        Require(timers.Garmoth is { Kills: >= 0 and <= GarmothTracker.Limit });
        BossRegions.Find(timers.SelectedBossRegion);
        Require(timers.BossRegions is not null && timers.BossRegions.All(r => r is not null)
            && timers.BossRegions.Select(r => r.RegionId).Distinct().Count() == timers.BossRegions.Count);
        foreach (var region in timers.BossRegions)
        {
            BossRegions.Find(region.RegionId);
            if (region.AcceptedBossTimetable is { } baseline) Timetable(baseline);
        }
        Require(timers.Timers.Count <= 10000 && timers.Timers.Select(t => t.Id).Distinct().Count() == timers.Timers.Count);
        foreach (var timer in timers.Timers)
        {
            Require(timer is not null && timer.Id != Guid.Empty && timer.Name is not null && Enum.IsDefined(timer.Kind));
            if (timer.IsBuiltIn) BossRegions.Find(BossRegions.RegionOf(timer));
            Require(!timer.AddedByUser || timer.IsBuiltIn && timer.Kind == TimerKind.Scheduled);
            Require(timer.Alerts is not null && timer.Alerts.Sound is not null && timer.Alerts.Toast is not null
                && timer.Alerts.Tts is not null && timer.Alerts.Overlay is not null);
            Require(timer.Alerts.LeadTimesMinutes is null || timer.Alerts.LeadTimesMinutes.All(m => m is >= 0 and <= 1440));
            Require(timer.Alerts.Tts.Template is not null && timer.Alerts.Tts.NowTemplate is not null);
            FileName(timer.ImageFile);
            if (timer.Kind == TimerKind.Scheduled)
            {
                Require(timer.Scheduled is not null && timer.Scheduled.Slots is not null);
                TimeZoneInfo.FindSystemTimeZoneById(timer.Scheduled.TimeZoneId);
                Require(timer.Scheduled.Slots.All(s => Enum.IsDefined(s.Day)));
                ScheduleMath.ValidateDateRange(timer.Scheduled.StartDate, timer.Scheduled.EndDate);
                ScheduleMath.ValidateEveryWeeks(timer.Scheduled.EveryWeeks);
            }
            else if (timer.Kind == TimerKind.OneTime)
            {
                Require(timer.OneTime is not null);
                OneTimeEvents.AtUtc(timer.OneTime);
            }
            else if (timer.Kind == TimerKind.Countdown)
            {
                var countdown = timer.Countdown;
                Require(countdown is not null && countdown.Duration > TimeSpan.Zero && Enum.IsDefined(countdown.Status));
                Require(countdown.Status != CountdownStatus.Running || countdown.EndsAtUtc is not null);
                Require(countdown.Status != CountdownStatus.Paused || countdown.Remaining is not null);
            }
            else
            {
                var stopwatch = timer.Stopwatch;
                Require(stopwatch is not null && Enum.IsDefined(stopwatch.Status));
                Require(stopwatch.Status != CountdownStatus.Running || stopwatch.StartedAtUtc is not null);
                Require(stopwatch.Status != CountdownStatus.Paused || stopwatch.Elapsed is not null);
            }
        }
        if (timers.AcceptedBossTimetable is { } seed)
            Timetable(seed);
    }

    static void Todos(TodoData todos)
    {
        Require(todos.DefaultsVersion is >= 0 and <= TodoData.CurrentDefaultsVersion && todos.Lists is not null);
        Require(todos.Lists.Select(l => l.Id).Distinct().Count() == todos.Lists.Count);
        foreach (var list in todos.Lists)
        {
            Require(list is not null && list.Id != Guid.Empty && list.Name is not null && Enum.IsDefined(list.Cadence));
            var rowIds = new HashSet<Guid>();
            Rows(list.Rows, rowIds, 0);
        }
    }

    static void Schedule(TodoSchedule schedule) => Require(schedule is not null && Enum.IsDefined(schedule.Day) && schedule.Hour is >= 0 and <= 23 && schedule.Minute is >= 0 and <= 59);

    static void Timetable(BossSeed seed)
    {
        Require(seed.Bosses is not null && seed.Bosses.All(b => b is not null && b.Name is not null && b.Slots is not null));
        Require(seed.Bosses.Select(b => b.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == seed.Bosses.Count
            && seed.Bosses.All(b => b.Slots.All(s => Enum.IsDefined(s.Day))));
        TimeZoneInfo.FindSystemTimeZoneById(seed.TimeZoneId);
        TimetableUpdates.Revision(seed);
    }

    static void Rows(IReadOnlyList<TodoRow> rows, HashSet<Guid> ids, int depth)
    {
        Require(rows is not null && depth <= 32);
        foreach (var row in rows)
        {
            Require(row is not null && row.Id != Guid.Empty && row.Text is not null && ids.Add(row.Id) && ids.Count <= 100000);
            Rows(row.Children, ids, depth + 1);
        }
    }

    static void FileName(string? name) => Require(name is null || BackupArchive.SafeFileName(name));
    static void Require([DoesNotReturnIf(false)] bool valid)
    {
        if (!valid) throw new InvalidDataException("The saved data is invalid or needs a newer app version.");
    }
}
