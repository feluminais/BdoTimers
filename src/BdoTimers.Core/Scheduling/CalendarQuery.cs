using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

public enum CalendarKind { Boss, Weekly, Event, Countdown, DailyReset, WeeklyReset }

/// <summary>A boss spawn, a weekly timer's occurrence, a one-time event, a running countdown's end or a to-do reset;
/// resets have no timer.</summary>
public sealed record CalendarItem(CalendarKind Kind, DateTimeOffset AtUtc, TimerDef? Timer, CellState State)
{
    public bool IsReset => Kind is CalendarKind.DailyReset or CalendarKind.WeeklyReset;
}

/// <summary>A local day of a month view; <see cref="InMonth"/> is false for the days before and after the month.</summary>
public sealed record CalendarDay(DateOnly Date, bool InMonth, IReadOnlyList<CalendarItem> Items);

/// <summary>Six Monday-first weeks covering a month, in local time.</summary>
public sealed record CalendarMonth(int Year, int Month, IReadOnlyList<CalendarDay> Days);

/// <summary>Everything the app schedules, for Today and the Schedule's Month. States follow the Week grid.</summary>
public static class CalendarQuery
{
    /// <summary>The month of <paramref name="year"/>/<paramref name="month"/> as 42 local days from the Monday on or before
    /// its first day; each day's items are in time order.</summary>
    public static CalendarMonth Month(AppData data, AppSettings settings, int year, int month, DateTimeOffset now,
        TimeZoneInfo local, BossBoardCache? boards = null)
    {
        var first = new DateOnly(year, month, 1);
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var fromUtc = ScheduleMath.LocalToUtc(start.ToDateTime(TimeOnly.MinValue), local);
        var toUtc = ScheduleMath.LocalToUtc(start.AddDays(42).ToDateTime(TimeOnly.MinValue), local);
        var byDay = Between(data, settings, fromUtc, toUtc, now, local, boards)
            .ToLookup(i => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(i.AtUtc, local).DateTime));
        return new(year, month, Enumerable.Range(0, 42)
            .Select(d => start.AddDays(d))
            .Select(date => new CalendarDay(date, date.Month == month, byDay[date].ToList()))
            .ToList());
    }

    /// <summary>Items in [fromUtc, toUtc): the selected region's bosses, the user's timers and both to-do resets, ordered
    /// by time, then kind, then name. Horse registrations under way count as one.</summary>
    public static IReadOnlyList<CalendarItem> Between(AppData data, AppSettings settings, DateTimeOffset fromUtc,
        DateTimeOffset toUtc, DateTimeOffset now, TimeZoneInfo? local = null, BossBoardCache? boards = null)
    {
        var muted = data.Muted.ToHashSet();
        var next = (boards?.Get(data, now) ?? BossBoard.Build(data, now)).Next?.AtUtc;
        var items = new List<CalendarItem>();
        foreach (var timer in data.Timers.Where(t => BossRegions.IsEligible(data, t)))
            foreach (var (kind, at) in Occurrences(timer, fromUtc, toUtc))
                items.Add(new(kind, at, timer, WeekGrid.StateOf(timer, at, now, kind == CalendarKind.Boss ? next : null, muted,
                    GarmothTracker.Gone(data, timer, at))));
        OneForRegistrations(data, items);
        items.AddRange(Resets(CalendarKind.DailyReset, TodoCadence.Daily, settings.DailyTodoReset, fromUtc, toUtc, now, local));
        items.AddRange(Resets(CalendarKind.WeeklyReset, TodoCadence.Weekly, settings.WeeklyTodoReset, fromUtc, toUtc, now, local));
        return items
            .OrderBy(i => i.AtUtc)
            .ThenBy(i => i.Kind)
            .ThenBy(i => i.Timer?.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The horse registrations under way are one entry, the next to end, under the Horse registration timer: ten of them would
    /// crowd out the rest, and the timer's panel lists them. Without that timer the next run stands as it is.
    /// </summary>
    static void OneForRegistrations(AppData data, List<CalendarItem> items)
    {
        var runs = items.Where(i => i.Timer?.Preset == Presets.HorseRegistrationRun).OrderBy(i => i.AtUtc).ToList();
        if (runs.Count == 0) return;
        items.RemoveAll(runs.Contains);
        var template = data.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration);
        items.Add(template is null ? runs[0] : runs[0] with { Timer = template });
    }

    static IEnumerable<(CalendarKind Kind, DateTimeOffset At)> Occurrences(TimerDef timer, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        switch (timer)
        {
            case { Kind: TimerKind.Scheduled, Scheduled: { Off: false } spec }:
                var kind = timer.IsBuiltIn ? CalendarKind.Boss : CalendarKind.Weekly;
                return ScheduleMath.From(spec, fromUtc).TakeWhile(at => at < toUtc).Select(at => (kind, at));
            // Finished events stay, as past days do.
            case { Kind: TimerKind.OneTime, OneTime: { } oneTime }
                when OneTimeEvents.AtUtc(oneTime) is var at && at >= fromUtc && at < toUtc:
                return [(CalendarKind.Event, at)];
            // A paused countdown has no end yet.
            case { Kind: TimerKind.Countdown, Countdown: { Status: CountdownStatus.Running, EndsAtUtc: { } end } }
                when end >= fromUtc && end < toUtc:
                return [(CalendarKind.Countdown, end)];
            default:
                return [];
        }
    }

    static IEnumerable<CalendarItem> Resets(CalendarKind kind, TodoCadence cadence, TodoSchedule schedule,
        DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTimeOffset now, TimeZoneInfo? local)
    {
        // Next is strictly after its argument, so start a tick early to include a reset exactly at fromUtc.
        for (var at = TodoReset.Next(cadence, schedule, fromUtc.AddTicks(-1), local); at < toUtc;
             at = TodoReset.Next(cadence, schedule, at, local))
            yield return new(kind, at, null, at <= now ? CellState.Past : CellState.Upcoming);
    }
}
