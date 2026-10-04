using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;

namespace BdoTimers.Core.Tests;

public sealed class RepeatScheduleTests
{
    static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value + "Z");
    static TimerDef Roses(string region = BossRegions.Europe) => Presets.Create(region).Single(t => t.Preset == Presets.WarOfTheRoses);

    [Theory]
    [InlineData("2026-09-20")]
    [InlineData("2026-11-01")]
    public void Anchor_before_or_after_range_keeps_the_same_running_weeks(string anchor)
    {
        var spec = Roses().Scheduled! with { WeekAnchor = DateOnly.Parse(anchor) };
        Assert.Equal(new[] { Utc("2026-10-04T13:05:00"), Utc("2026-10-04T15:00:00"),
            Utc("2026-10-18T13:05:00"), Utc("2026-10-18T15:00:00") },
            ScheduleMath.From(spec, Utc("2026-10-01T00:00:00")).Take(4));
        Assert.Empty(ScheduleMath.From(spec with { StartDate = new(2026, 10, 5), EndDate = new(2026, 10, 11) },
            Utc("2026-10-01T00:00:00")));
    }

    [Fact]
    public void Anchor_week_runs_and_NA_applications_share_its_Monday_first_week()
    {
        var spec = Roses(BossRegions.NorthAmerica).Scheduled! with
        {
            Slots = [new(DayOfWeek.Friday, new(23, 0), "Guild applications"),
                new(DayOfWeek.Saturday, new(23, 10), "Third Legion"), new(DayOfWeek.Sunday, new(15, 0), "Battle")],
        };
        Assert.Equal(new[] { Utc("2026-09-19T06:00:00"), Utc("2026-09-20T06:10:00"), Utc("2026-09-20T22:00:00"),
            Utc("2026-10-03T06:00:00") }, ScheduleMath.From(spec, Utc("2026-09-14T00:00:00")).Take(4));
    }

    [Theory]
    [InlineData("eu", "2026-10-18T15:00:00", "2026-11-01T16:00:00")]
    [InlineData("na", "2026-10-18T22:00:00", "2026-11-01T23:00:00")]
    [InlineData("eu", "2026-03-22T16:00:00", "2026-04-05T15:00:00")]
    [InlineData("na", "2026-03-08T22:00:00", "2026-03-22T22:00:00")]
    public void Repeating_battles_keep_server_wall_time_across_DST(string region, string first, string second)
    {
        var spec = Roses(region).Scheduled!;
        spec = spec with { Slots = spec.Slots.Where(s => s.Label == "Battle").ToList() };
        Assert.Equal(new[] { Utc(first), Utc(second) }, ScheduleMath.From(spec, Utc(first)).Take(2));
    }

    [Fact]
    public void Labels_name_the_occurrence_and_spoken_alert()
    {
        var timer = Roses();
        var at = Utc("2026-10-04T15:00:00");
        Assert.Equal("Battle", ScheduleMath.SlotAt(timer.Scheduled!, at)?.Label);
        Assert.Null(ScheduleMath.SlotAt(timer.Scheduled!, at.AddMinutes(1)));
        Assert.Equal("War of the Roses · Battle", OccurrenceSource.NameAt(timer, at));
        Assert.Equal("War of the Roses", OccurrenceSource.NameAt(timer, at.AddMinutes(1)));
        var message = AlertMessage.Build(new([timer], at, 5, 5));
        Assert.Equal("War of the Roses · Battle", message.Title);
        Assert.Contains("War of the Roses, Battle", message.Speech);
        Assert.DoesNotContain("·", message.Speech);
    }

    [Fact]
    public void Spring_gap_occurrence_keeps_its_label()
    {
        var timer = Roses() with { Scheduled = new() { TimeZoneId = "Europe/Berlin",
            Slots = [new(DayOfWeek.Sunday, new(2, 30), "Registration")] } };
        var at = Utc("2026-03-29T01:30:00");
        Assert.Equal("War of the Roses · Registration", OccurrenceSource.NameAt(timer, at));
    }

    [Fact]
    public void Fifty_two_week_repeat_finds_the_next_year()
    {
        var spec = new ScheduledSpec { EveryWeeks = 52, WeekAnchor = new(2026, 10, 4),
            Slots = [new(DayOfWeek.Sunday, new(17, 0))] };
        Assert.Equal(Utc("2027-10-03T17:00:00"), ScheduleMath.From(spec, Utc("2026-10-05T00:00:00")).First());
    }

    [Fact]
    public void Skipped_last_week_ends_without_date_overflow()
    {
        var spec = new ScheduledSpec { EveryWeeks = 2, WeekAnchor = new(9999, 12, 20),
            Slots = [new(DayOfWeek.Sunday, new(17, 0))] };
        Assert.Empty(ScheduleMath.From(spec, Utc("9999-12-27T00:00:00")));
    }

    [Fact]
    public void Overlay_waits_for_the_actual_next_fortnight()
    {
        var timer = Roses() with { Alerts = new() { Overlay = new() { Enabled = true, ShowMinutesBefore = 10 } } };
        Assert.Equal(Utc("2026-10-18T12:55:00"), UpcomingQuery.OverlayStart(new() { Timers = [timer] },
            new(), Utc("2026-10-05T00:00:00")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public void Store_and_saved_data_reject_invalid_repeat(int weeks)
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var store = new TimerStore(file, new());
        var timer = Roses();
        store.Upsert(timer);
        Assert.Throws<ArgumentException>(() => store.SetRepeat(timer.Id, weeks, new(2026, 10, 4)));
        var invalid = timer with { Scheduled = timer.Scheduled! with { EveryWeeks = weeks } };
        Assert.Throws<ArgumentException>(() => store.Upsert(invalid));
        var invalidFile = new JsonFileStore<AppData>(dir.File("invalid.json"), () => new());
        invalidFile.Save(new() { Timers = [invalid] });
        Assert.NotNull(invalidFile.Load().RecoveredBackupPath);
        Assert.Equal(2, store.Current.Timers.Single().Scheduled!.EveryWeeks);
    }

    [Fact]
    public void Repeat_edit_is_persisted()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var store = new TimerStore(file, new());
        var timer = Roses();
        store.Upsert(timer);
        store.SetRepeat(timer.Id, 3, new(2026, 10, 4));
        var saved = file.Load().Value.Timers.Single().Scheduled!;
        Assert.Equal(3, saved.EveryWeeks);
        Assert.Equal(new DateOnly(2026, 10, 4), saved.WeekAnchor);
    }

    [Fact]
    public void Region_moves_defaults_preserves_edits_and_reset_restores_schedule_only()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var timer = Roses() with { Name = "My roses", Enabled = false, ImageFile = "roses.jpg", Alerts = new() { LeadTimesMinutes = [3] } };
        var store = new TimerStore(file, new() { Timers = [timer] });
        var clock = new FakeClock(Utc("2026-10-04T00:00:00"));
        store.SelectBossRegion(BossRegions.NorthAmerica, clock);
        var moved = store.Current.Timers.Single(t => t.Id == timer.Id);
        Assert.Equal("America/Los_Angeles", moved.Scheduled!.TimeZoneId);
        Assert.Equal(new TimeOnly(15, 0), moved.Scheduled.Slots.Single(s => s.Label == "Battle").Time);
        store.SetRepeat(timer.Id, 3, new(2026, 10, 4));
        store.SelectBossRegion(BossRegions.Europe, clock);
        Assert.Equal(3, store.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.EveryWeeks);
        Assert.Equal("America/Los_Angeles", store.Current.Timers.Single(t => t.Id == timer.Id).Scheduled!.TimeZoneId);
        store.ResetWarOfTheRoses();
        var reset = store.Current.Timers.Single(t => t.Id == timer.Id);
        Assert.Equal("Europe/Berlin", reset.Scheduled!.TimeZoneId);
        Assert.Equal(2, reset.Scheduled.EveryWeeks);
        Assert.Equal(new DateOnly(2026, 9, 20), reset.Scheduled.WeekAnchor);
        Assert.Equal(timer.Alerts, reset.Alerts);
        Assert.Equal(timer.Name, reset.Name);
        Assert.Equal(timer.ImageFile, reset.ImageFile);
        Assert.False(reset.Enabled);
    }
}
