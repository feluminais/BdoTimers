using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimers;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class ComingUpTests
{
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    static readonly AppSettings Settings = new();

    // T0 is Tuesday 12:00 UTC; the first boss is 2 hours away.
    static readonly TimerDef Kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 16);
    static readonly TimerDef Uturi = Boss("Uturi", DayOfWeek.Tuesday, 19) with { Enabled = false };
    static readonly TimerDef Garmoth = Boss("Garmoth", DayOfWeek.Thursday, 23, 15);

    static IReadOnlyList<CalendarItem> At(DateTimeOffset now, params TimerDef[] timers) =>
        ComingUp.Between(new AppData { Timers = timers }, Settings, now, Berlin);

    static IReadOnlyList<CalendarItem> At(AppData data, DateTimeOffset now) => ComingUp.Between(data, Settings, now, Berlin);

    [Fact]
    public void Bosses_with_alerts_off_stay_out_of_the_list_and_bosses_past_the_window_too()
    {
        var items = At(T0, Kzarka, Uturi, Garmoth);

        Assert.Equal(["Kzarka"], items.Where(i => i.Kind == CalendarKind.Boss).Select(i => i.Timer!.Name));
    }

    [Fact]
    public void A_skipped_spawn_stays_in_the_list_marked_skipped()
    {
        var data = new AppData { Timers = [Kzarka], Muted = [new MutedOccurrence(Kzarka.Id, Utc(22, 14, 0))] };

        var item = Assert.Single(At(data, T0), i => i.Kind == CalendarKind.Boss);

        Assert.Equal(CellState.Skipped, item.State);
    }

    [Fact]
    public void Bosses_that_spawn_together_share_a_row_and_anything_else_keeps_its_own()
    {
        var nouver = Boss("Nouver", DayOfWeek.Tuesday, 16);
        var garmoth = Boss("Garmoth", DayOfWeek.Tuesday, 17);
        var guild = Scheduled("Guild bosses", DayOfWeek.Tuesday, 18, 0);
        var farm = Countdown(T0.AddHours(4));

        var rows = ComingUp.Rows(At(T0, Kzarka, nouver, garmoth, guild, farm));

        var bosses = rows.Where(row => row[0].Kind == CalendarKind.Boss).ToList();
        Assert.Equal([["Kzarka", "Nouver"], ["Garmoth"]], bosses.Select(row => row.Select(i => i.Timer!.Name).ToArray()));
        Assert.All(rows, row => Assert.Single(row.Select(i => i.AtUtc).Distinct()));
        Assert.All(rows.Where(row => row[0].Kind != CalendarKind.Boss), row => Assert.Single(row));
        Assert.Equal(rows.SelectMany(row => row).Select(i => i.AtUtc).Order(), rows.SelectMany(row => row).Select(i => i.AtUtc));
        Assert.Equal(At(T0, Kzarka, nouver, garmoth, guild, farm).Count, rows.Sum(row => row.Count));
    }

    [Fact]
    public void A_boss_and_a_timer_at_the_same_time_stay_in_separate_rows()
    {
        var weekly = Scheduled("Guild bosses", DayOfWeek.Tuesday, 16, 0);

        var rows = ComingUp.Rows(At(T0, Kzarka, weekly)).Where(row => row[0].AtUtc == T0.AddHours(2)).ToList();

        Assert.Equal([CalendarKind.Boss, CalendarKind.Weekly], rows.Select(row => Assert.Single(row).Kind));
    }

    [Fact]
    public void The_next_boss_is_marked_next()
    {
        var item = Assert.Single(At(T0, Kzarka), i => i.Kind == CalendarKind.Boss);

        Assert.Equal(CellState.Next, item.State);
    }

    [Fact]
    public void The_window_is_the_next_24_hours()
    {
        // Wednesday 14:00 Berlin is Wednesday 12:00 UTC, exactly 24 hours after T0.
        var weekly = Scheduled("Guild bosses", DayOfWeek.Wednesday, 14, 0);

        Assert.DoesNotContain(At(T0, weekly), i => i.Timer?.Id == weekly.Id);
        Assert.Contains(At(T0.AddSeconds(1), weekly), i => i.Timer?.Id == weekly.Id);
    }

    [Fact]
    public void Own_weekly_timers_events_countdown_ends_and_the_daily_reset_show_in_time_order()
    {
        var weekly = Scheduled("Guild bosses", DayOfWeek.Tuesday, 21, 0);
        var farm = Countdown(T0.AddHours(3));
        var party = new TimerDef
        {
            Name = "Party", Kind = TimerKind.OneTime,
            OneTime = new OneTimeSpec { Date = new DateOnly(2026, 9, 22), Time = new TimeOnly(20, 0), TimeZoneId = "Europe/Berlin" },
        };

        var items = At(T0, weekly, farm, party);

        Assert.Equal([CalendarKind.Countdown, CalendarKind.Event, CalendarKind.Weekly, CalendarKind.DailyReset],
            items.Select(i => i.Kind));
        Assert.Equal(items.OrderBy(i => i.AtUtc).Select(i => i.AtUtc), items.Select(i => i.AtUtc));
    }

    [Fact]
    public void The_weekly_reset_shows_when_it_falls_inside_the_window()
    {
        Assert.DoesNotContain(At(T0), i => i.Kind == CalendarKind.WeeklyReset);
        // Thursday 00:00 UTC is 11 hours after Wednesday 13:00.
        Assert.Contains(At(Utc(23, 13, 0)), i => i.Kind == CalendarKind.WeeklyReset);
    }
}
