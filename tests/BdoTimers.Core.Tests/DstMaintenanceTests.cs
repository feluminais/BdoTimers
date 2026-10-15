using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

/// <summary>Pearl Abyss moves boss times for daylight saving at a maintenance; the clocks change on another day.</summary>
public sealed class DstMaintenanceTests : IDisposable
{
    readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    const string Berlin = "Europe/Berlin", Pacific = "America/Los_Angeles";
    static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);
    static DateTimeOffset Midnight(string date) => DateTimeOffset.Parse(date + "T00:00Z");

    static ScheduledSpec Spec(string zone, DateOnly maintenance, DayOfWeek day, int hour, int minute = 0) => new()
    {
        TimeZoneId = zone, Slots = [new Slot(day, new TimeOnly(hour, minute), "Spawn")],
        ServerSwitchUtc = ScheduleMath.ServerSwitch(maintenance, zone),
    };

    static DateTimeOffset First(ScheduledSpec spec, DateTimeOffset fromUtc) => ScheduleMath.From(spec, fromUtc).First();

    // Berlin's clocks go back on Sun 25 Oct 2026 at 01:00 UTC; the server keeps CEST until the Thu 29 Oct maintenance.
    [Theory]
    [InlineData(DayOfWeek.Friday, 22, 15, "2026-10-23", "2026-10-23T20:15Z")]   // before the clock change: CEST either way
    [InlineData(DayOfWeek.Sunday, 22, 15, "2026-10-25", "2026-10-25T20:15Z")]   // the zone alone says 21:15
    [InlineData(DayOfWeek.Sunday, 2, 30, "2026-10-25", "2026-10-25T00:30Z")]    // the zone's repeated hour, once, in CEST
    [InlineData(DayOfWeek.Thursday, 2, 0, "2026-10-29", "2026-10-29T00:00Z")]   // Thursday before the maintenance
    [InlineData(DayOfWeek.Thursday, 14, 0, "2026-10-29", "2026-10-29T13:00Z")]  // Thursday after it: CET
    [InlineData(DayOfWeek.Friday, 22, 15, "2026-10-30", "2026-10-30T21:15Z")]   // after the window
    public void Europe_keeps_summer_time_until_the_maintenance(DayOfWeek day, int hour, int minute, string date, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected),
            First(Spec(Berlin, new DateOnly(2026, 10, 29), day, hour, minute), Midnight(date).AddHours(-3)));

    // Pacific clocks go back on Sun 1 Nov 2026 at 09:00 UTC; the server takes PST at the Thu 29 Oct maintenance.
    [Theory]
    [InlineData(DayOfWeek.Wednesday, 22, 15, "2026-10-28", "2026-10-29T05:15Z")] // before the maintenance: PDT
    [InlineData(DayOfWeek.Thursday, 0, 0, "2026-10-29", "2026-10-29T07:00Z")]    // Thursday before the maintenance
    [InlineData(DayOfWeek.Thursday, 10, 0, "2026-10-29", "2026-10-29T18:00Z")]   // after it: the zone alone says 17:00
    [InlineData(DayOfWeek.Sunday, 0, 0, "2026-11-01", "2026-11-01T08:00Z")]      // the last hour before the clocks change
    [InlineData(DayOfWeek.Sunday, 1, 30, "2026-11-01", "2026-11-01T09:30Z")]     // the zone's repeated hour, once, in PST
    [InlineData(DayOfWeek.Monday, 10, 0, "2026-11-02", "2026-11-02T18:00Z")]     // after the window
    public void North_America_takes_winter_time_at_the_maintenance(DayOfWeek day, int hour, int minute, string date, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected),
            First(Spec(Pacific, new DateOnly(2026, 10, 29), day, hour, minute), Midnight(date)));

    [Fact]
    public void Spring_2026_matches_the_notices()
    {
        // NA: after the 5 Mar maintenance until 8 Mar, a noon PST boss spawns at 11:00 PST.
        Assert.Equal(Utc(3, 6, 19), First(Spec(Pacific, new(2026, 3, 5), DayOfWeek.Friday, 12), Utc(3, 6, 0)));
        Assert.Equal(Utc(3, 4, 20), First(Spec(Pacific, new(2026, 3, 5), DayOfWeek.Wednesday, 12), Utc(3, 4, 0)));
        // EU: after the 26 Mar maintenance until 29 Mar, a 16:00 CET boss spawns at 15:00 CET.
        Assert.Equal(Utc(3, 27, 14), First(Spec(Berlin, new(2026, 3, 26), DayOfWeek.Friday, 16), Utc(3, 27, 0)));
        Assert.Equal(Utc(3, 30, 14), First(Spec(Berlin, new(2026, 3, 26), DayOfWeek.Monday, 16), Utc(3, 30, 0)));
    }

    [Fact]
    public void Only_a_date_near_a_clock_change_switches()
    {
        Assert.Equal(Utc(10, 29, 10), ScheduleMath.ServerSwitch(new DateOnly(2026, 10, 29), Berlin));
        Assert.Null(ScheduleMath.ServerSwitch(new DateOnly(2026, 7, 16), Berlin));
        Assert.Null(ScheduleMath.ServerSwitch(DateOnly.MaxValue, Berlin));
        Assert.Null(ScheduleMath.ServerSwitch(DateOnly.MinValue, Pacific));
    }

    [Fact]
    public void A_moved_spawn_keeps_its_label()
    {
        var spec = Spec(Berlin, new DateOnly(2026, 10, 29), DayOfWeek.Sunday, 22, 15);
        Assert.Equal("Spawn", ScheduleMath.SlotAt(spec, Utc(10, 25, 20, 15))?.Label);
        Assert.Null(ScheduleMath.SlotAt(spec, Utc(10, 25, 21, 15)));
    }

    TimerStore Store(AppData data) => new(new JsonFileStore<AppData>(_dir.File("timers.json"), () => new AppData()), data);

    static AppData Data(params TimerDef[] timers) => new() { Timers = timers, SelectedBossRegion = BossRegions.Europe };

    [Fact]
    public void The_store_moves_server_time_schedules_of_that_region_only()
    {
        var war = new TimerDef
        {
            Name = "War of the Roses", Kind = TimerKind.Scheduled, Preset = Presets.WarOfTheRoses,
            Scheduled = Presets.WarOfTheRosesSchedule(BossRegions.NorthAmerica),
        };
        var own = TestTimers.Scheduled("Mine", DayOfWeek.Sunday, 22, 15) with
        {
            Scheduled = new ScheduledSpec { TimeZoneId = "W. Europe Standard Time", Slots = [new(DayOfWeek.Sunday, new(22, 15))] },
        };
        var store = Store(Data(TestTimers.Boss("Kzarka", DayOfWeek.Sunday, 22, 15), war, own));

        store.SetDstMaintenance(BossRegions.Europe, new DateOnly(2026, 10, 29));

        var timers = store.Current.Timers;
        Assert.Equal(Utc(10, 29, 10), timers[0].Scheduled!.ServerSwitchUtc);
        Assert.Null(timers[1].Scheduled!.ServerSwitchUtc);
        Assert.Null(timers[2].Scheduled!.ServerSwitchUtc);
        Assert.Equal(Utc(10, 25, 20, 15), OccurrenceSource.Next(timers[0], Utc(10, 25, 0)));
        Assert.Equal(Utc(10, 25, 21, 15), OccurrenceSource.Next(timers[2], Utc(10, 25, 0)));

        store.SetDstMaintenance(BossRegions.NorthAmerica, new DateOnly(2026, 10, 29));
        Assert.Equal(Utc(10, 29, 10), store.Current.Timers[1].Scheduled!.ServerSwitchUtc);

        store.SetDstMaintenance(BossRegions.Europe, null);
        Assert.Null(store.Current.Timers[0].Scheduled!.ServerSwitchUtc);
        Assert.Equal(Utc(10, 29, 10), store.Current.Timers[1].Scheduled!.ServerSwitchUtc);
    }

    [Fact]
    public void The_date_is_saved_and_the_switch_is_not()
    {
        var store = Store(Data(TestTimers.Boss("Kzarka", DayOfWeek.Sunday, 22, 15)));
        store.SetDstMaintenance(BossRegions.Europe, new DateOnly(2026, 10, 29));

        var json = File.ReadAllText(_dir.File("timers.json"));
        Assert.Contains("2026-10-29", json);
        Assert.DoesNotContain("serverSwitch", json, StringComparison.OrdinalIgnoreCase);

        var reloaded = Store(new JsonFileStore<AppData>(_dir.File("timers.json"), () => new AppData()).Load().Value);
        Assert.Equal(Utc(10, 29, 10), reloaded.Current.Timers[0].Scheduled!.ServerSwitchUtc);
    }

    [Fact]
    public void A_boss_added_later_follows_the_date()
    {
        var store = Store(Data());
        store.SetDstMaintenance(BossRegions.Europe, new DateOnly(2026, 10, 29));
        store.Upsert(TestTimers.Boss("Vell", DayOfWeek.Sunday, 22, 15));
        Assert.Equal(Utc(10, 29, 10), store.Current.Timers[0].Scheduled!.ServerSwitchUtc);
    }

    [Fact]
    public void The_date_shows_until_two_weeks_after_it()
    {
        var data = BossRegions.WithState(Data(), new BossRegionState { RegionId = BossRegions.Europe, DstMaintenance = new(2026, 10, 29) });
        Assert.Equal(new DateOnly(2026, 10, 29), BossRegions.ActiveDstMaintenance(data, BossRegions.Europe, Utc(10, 20, 0)));
        Assert.Equal(new DateOnly(2026, 10, 29), BossRegions.ActiveDstMaintenance(data, BossRegions.Europe, Utc(11, 12, 9)));
        Assert.Null(BossRegions.ActiveDstMaintenance(data, BossRegions.Europe, Utc(11, 12, 10)));
        Assert.Null(BossRegions.ActiveDstMaintenance(data, BossRegions.NorthAmerica, Utc(10, 20, 0)));
    }

    [Fact]
    public void Alerts_board_calendar_overlay_and_garmoth_agree()
    {
        var kzarka = TestTimers.Boss("Kzarka", DayOfWeek.Sunday, 22, 15) with { Alerts = new AlertConfig { LeadTimesMinutes = [5] } };
        var garmoth = TestTimers.Boss(GarmothTracker.BossName, DayOfWeek.Sunday, 22, 15);
        var store = Store(Data(kzarka, garmoth) with
        {
            Garmoth = new GarmothWeek { Kills = GarmothTracker.Limit, DoneAtUtc = Utc(10, 23, 0), ResetUtc = Utc(10, 29, 0) },
        });
        store.SetDstMaintenance(BossRegions.Europe, new DateOnly(2026, 10, 29));
        var data = store.Current;
        var spawn = Utc(10, 25, 20, 15);
        var now = spawn.AddMinutes(-10);

        Assert.Equal(spawn, BossBoard.Build(data, now).Next?.AtUtc);
        Assert.Contains(CalendarQuery.Between(data, new AppSettings(), now, now.AddHours(1), now),
            i => i.Timer?.Id == kzarka.Id && i.AtUtc == spawn);
        var listed = UpcomingQuery.Listed(data, new OverlaySettings { Timers = [new(kzarka.Id, 0, Always: true)] }, now);
        Assert.Equal(spawn, Assert.Single(listed).AtUtc);
        var silenced = GarmothTracker.Silenced(data);
        Assert.Contains(new MutedOccurrence(garmoth.Id, spawn), silenced);
        var alert = Assert.Single(new AlertPlanner().Tick(data.Timers, silenced, spawn.AddMinutes(-5)));
        Assert.Equal((kzarka.Id, spawn), (alert.Timers.Single().Id, alert.OccurrenceUtc));
    }
}
