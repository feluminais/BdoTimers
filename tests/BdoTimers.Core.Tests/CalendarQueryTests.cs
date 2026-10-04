using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using static BdoTimers.Core.Tests.TestTimers;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class CalendarQueryTests
{
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    static readonly AppSettings Settings = new();

    static readonly TimerDef Kzarka = Boss("Kzarka", DayOfWeek.Monday, 22, 15);
    static readonly TimerDef Nouver = Boss("Nouver", DayOfWeek.Tuesday, 16);
    static readonly TimerDef Uturi = Boss("Uturi", DayOfWeek.Tuesday, 19) with { Enabled = false };

    static CalendarDay Day(CalendarMonth month, int monthNumber, int day) =>
        month.Days.Single(d => d.Date == new DateOnly(2026, monthNumber, day));

    static IEnumerable<CalendarItem> Of(CalendarDay day, CalendarKind kind) => day.Items.Where(i => i.Kind == kind);

    [Fact]
    public void A_month_is_six_monday_first_weeks_around_it()
    {
        var month = CalendarQuery.Month(new AppData(), Settings, 2026, 9, T0, Berlin);

        Assert.Equal(42, month.Days.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), month.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 11), month.Days[^1].Date);
        Assert.False(month.Days[0].InMonth);
        Assert.True(month.Days[1].InMonth);
        Assert.Equal(30, month.Days.Count(d => d.InMonth));
    }

    [Fact]
    public void Boss_spawns_carry_the_week_grid_states()
    {
        var data = new AppData { Timers = [Kzarka, Nouver, Uturi], Muted = [new(Nouver.Id, Utc(29, 14, 0))] };

        var month = CalendarQuery.Month(data, Settings, 2026, 9, T0, Berlin);

        Assert.Equal(CellState.Past, Of(Day(month, 9, 21), CalendarKind.Boss).Single().State);
        Assert.Equal(CellState.Next, Of(Day(month, 9, 22), CalendarKind.Boss).Single(i => i.Timer!.Name == "Nouver").State);
        Assert.Equal(CellState.Unfollowed, Of(Day(month, 9, 22), CalendarKind.Boss).Single(i => i.Timer!.Name == "Uturi").State);
        Assert.Equal(CellState.Upcoming, Of(Day(month, 9, 28), CalendarKind.Boss).Single().State);
        Assert.Equal(CellState.Skipped, Of(Day(month, 9, 29), CalendarKind.Boss).Single(i => i.Timer!.Name == "Nouver").State);
        Assert.Equal(6, month.Days.Sum(d => Of(d, CalendarKind.Boss).Count(i => i.Timer!.Name == "Kzarka")));
    }

    [Fact]
    public void Only_the_selected_region_s_bosses_show()
    {
        var na = Boss("Kzarka", DayOfWeek.Monday, 12) with { BossRegionId = BossRegions.NorthAmerica };

        var month = CalendarQuery.Month(new AppData { Timers = [Kzarka, na] }, Settings, 2026, 9, T0, Berlin);

        Assert.All(month.Days.SelectMany(d => Of(d, CalendarKind.Boss)), i => Assert.Same(Kzarka, i.Timer));
    }

    [Fact]
    public void Weekly_timers_events_and_running_countdowns_show_with_their_kind()
    {
        var weekly = Scheduled("Guild war", DayOfWeek.Wednesday, 21, 0) with
        {
            Scheduled = Scheduled("Guild war", DayOfWeek.Wednesday, 21, 0).Scheduled! with { EndDate = new DateOnly(2026, 9, 30) },
        };
        var finished = new TimerDef
        {
            Name = "Siege", Kind = TimerKind.OneTime,
            OneTime = new OneTimeSpec { Date = new(2026, 9, 20), Time = new(20, 0), TimeZoneId = "Europe/Berlin", Finished = true },
        };
        var running = Countdown(Utc(23, 8, 0));
        var paused = Countdown(Utc(24, 8, 0)) with { Countdown = running.Countdown! with { Status = CountdownStatus.Paused } };
        var data = new AppData { Timers = [weekly, finished, running, paused] };

        var month = CalendarQuery.Month(data, Settings, 2026, 9, T0, Berlin);

        Assert.Equal(new DateOnly[] { new(2026, 9, 2), new(2026, 9, 9), new(2026, 9, 16), new(2026, 9, 23), new(2026, 9, 30) },
            month.Days.Where(d => Of(d, CalendarKind.Weekly).Any()).Select(d => d.Date));
        Assert.Equal(CellState.Past, Of(Day(month, 9, 20), CalendarKind.Event).Single().State);
        Assert.Same(running, Of(Day(month, 9, 23), CalendarKind.Countdown).Single().Timer);
        Assert.Single(month.Days.SelectMany(d => Of(d, CalendarKind.Countdown)));
    }

    [Fact]
    public void Resets_follow_the_to_do_schedules()
    {
        var month = CalendarQuery.Month(new AppData(), Settings, 2026, 9, T0, Berlin);

        // 00:00 UTC is 02:00 in Berlin's summer time, every day; the weekly one on Thursdays.
        Assert.All(month.Days, d => Assert.Equal(new TimeOnly(2, 0),
            TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(Of(d, CalendarKind.DailyReset).Single().AtUtc, Berlin).DateTime)));
        Assert.All(month.Days.Where(d => Of(d, CalendarKind.WeeklyReset).Any()), d => Assert.Equal(DayOfWeek.Thursday, d.Date.DayOfWeek));
        Assert.Equal(6, month.Days.Sum(d => Of(d, CalendarKind.WeeklyReset).Count()));
        Assert.All(Day(month, 9, 21).Items, i => Assert.Equal(CellState.Past, i.State));
        Assert.Null(Of(Day(month, 9, 21), CalendarKind.DailyReset).Single().Timer);
    }

    [Fact]
    public void Spawns_land_on_their_local_day_across_the_autumn_clock_change()
    {
        var sunday = Boss("Vell", DayOfWeek.Sunday, 12);
        var late = Boss("Offin", DayOfWeek.Monday, 0, 15);
        var data = new AppData { Timers = [sunday, late] };

        var month = CalendarQuery.Month(data, Settings, 2026, 10, T0, Berlin);

        // Berlin leaves summer time on 2026-10-25.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 11, 0, 0, TimeSpan.Zero), Of(Day(month, 10, 25), CalendarKind.Boss).Single().AtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 23, 15, 0, TimeSpan.Zero), Of(Day(month, 10, 26), CalendarKind.Boss).Single().AtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero), Of(Day(month, 10, 18), CalendarKind.Boss).Single().AtUtc);
    }

    [Fact]
    public void Items_of_a_day_are_in_time_order()
    {
        var data = new AppData { Timers = [Kzarka, Nouver, Scheduled("Guild war", DayOfWeek.Tuesday, 21, 0)] };

        var items = CalendarQuery.Between(data, Settings, Utc(22, 0, 0), Utc(23, 0, 0), T0, Berlin);

        // The reset at 00:00 UTC, Nouver at 16:00 and the guild war at 21:00 Berlin.
        Assert.Equal(new[] { CalendarKind.DailyReset, CalendarKind.Boss, CalendarKind.Weekly },
            items.Select(i => i.Kind));
        Assert.True(items.Zip(items.Skip(1)).All(p => p.First.AtUtc <= p.Second.AtUtc));
    }
}
