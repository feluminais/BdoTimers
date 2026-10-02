using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public sealed class DatedTimersTests : IDisposable
{
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(Now);
    readonly Sink _sink = new();
    readonly JsonFileStore<AppData> _file;
    readonly TimerStore _store;
    readonly PersistentState<AppSettings> _settings;
    readonly SchedulerEngine _engine;

    sealed class Sink : IAlertSink
    {
        public List<AlertEvent> Alerts { get; } = [];
        public List<TimerDef> Missed { get; } = [];
        public void Dispatch(AlertEvent alert) => Alerts.Add(alert);
        public void NotifyEndedWhileAway(TimerDef timer) => Missed.Add(timer);
        public void PrepareSpeech(IReadOnlyCollection<string> texts) { }
    }

    public DatedTimersTests()
    {
        _file = new(_dir.File("timers.json"), () => new());
        _store = new(_file, new());
        _settings = new(new(_dir.File("settings.json"), () => new()), new());
        _engine = new(_store, _settings, _sink, _clock);
    }

    public void Dispose() => _dir.Dispose();

    static TimerDef Event(int minute = 10) => new()
    {
        Name = "Node War", Kind = TimerKind.OneTime,
        OneTime = new() { Date = new(2026, 10, 2), Time = new(12, minute), TimeZoneId = "UTC" },
        Alerts = new() { LeadTimesMinutes = [5, 0], Overlay = new() { Enabled = true, ShowMinutesBefore = 5 } },
    };

    void Tick(DateTimeOffset at) { _clock.UtcNow = at; _engine.Tick(); }

    [Fact]
    public void Event_alerts_once_then_stays_finished_even_after_clock_rollback_and_restart()
    {
        var timer = Event();
        _store.Upsert(timer);
        Tick(Now.AddMinutes(5));
        Tick(Now.AddMinutes(10));
        Tick(Now.AddDays(7));
        Assert.Equal([5, 0], _sink.Alerts.Select(a => a.LeadMinutes));
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
        Assert.Empty(_sink.Missed);

        var reloaded = new TimerStore(_file, _file.Load().Value);
        _clock.UtcNow = Now;
        var restarted = new SchedulerEngine(reloaded, _settings, _sink, _clock);
        restarted.ReconcileStartup();
        _clock.UtcNow = Now.AddMinutes(10);
        restarted.Tick();
        Assert.Equal(2, _sink.Alerts.Count);
        Assert.Empty(_sink.Missed);
        Assert.Empty(OccurrenceSource.Between(reloaded.Current.Timers.Single(), Now, Now.AddYears(1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(120)]
    public void Startup_finishes_missed_events_even_inside_grace_and_notices_only_once(int secondsLate)
    {
        var timer = Event();
        _store.Upsert(timer);
        _clock.UtcNow = Now.AddMinutes(10).AddSeconds(secondsLate);
        _engine.ReconcileStartup();
        _engine.Tick();
        var reloaded = new TimerStore(_file, _file.Load().Value);
        new SchedulerEngine(reloaded, _settings, _sink, _clock).ReconcileStartup();
        Assert.Equal(timer.Id, _sink.Missed.Single().Id);
        Assert.Empty(_sink.Alerts);
        Assert.True(reloaded.Current.Timers.Single().OneTime!.Finished);
    }

    [Fact]
    public void Startup_at_exact_occurrence_fires_normally()
    {
        _store.Upsert(Event());
        _clock.UtcNow = Now.AddMinutes(10);
        _engine.ReconcileStartup();
        _engine.Tick();
        Assert.Equal(0, _sink.Alerts.Single().LeadMinutes);
        Assert.Empty(_sink.Missed);
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
    }

    [Fact]
    public void Sleep_past_grace_finishes_with_one_missed_notice()
    {
        _store.Upsert(Event());
        Tick(Now.AddMinutes(5));
        Tick(Now.AddMinutes(20));
        Tick(Now.AddMinutes(21));
        Assert.Equal(5, _sink.Alerts.Single().LeadMinutes);
        Assert.Single(_sink.Missed);
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Event_completes_independently_of_alerts_enabled_or_paused(bool paused)
    {
        _store.Upsert(Event() with { Enabled = paused });
        if (paused) _settings.Update(s => s with { AlertsPausedUntilUtc = DateTimeOffset.MaxValue });
        Tick(Now.AddMinutes(10));
        Assert.Empty(_sink.Alerts);
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
        _settings.Update(s => s with { AlertsPausedUntilUtc = null });
        Tick(Now.AddDays(7));
        Assert.Empty(_sink.Alerts);
    }

    [Fact]
    public void Future_reschedule_rearms_but_renaming_finished_event_does_not()
    {
        var timer = Event();
        _store.Upsert(timer);
        Tick(Now.AddMinutes(10));
        _store.Modify(timer.Id, t => t with { Name = "Renamed" });
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
        _store.RescheduleEvent(timer.Id, new(2026, 10, 3), new(12, 10), "UTC", _clock);
        Assert.False(_store.Current.Timers.Single().OneTime!.Finished);
        Tick(Now.AddDays(1).AddMinutes(5));
        Tick(Now.AddDays(1).AddMinutes(10));
        Assert.Equal([0, 5, 0], _sink.Alerts.Select(a => a.LeadMinutes));
        Assert.True(_file.Load().Value.Timers.Single().OneTime!.Finished);
    }

    [Fact]
    public void Editing_finished_event_to_another_past_date_keeps_it_finished()
    {
        var timer = Event();
        _store.Upsert(timer);
        Tick(Now.AddMinutes(10));
        _store.RescheduleEvent(timer.Id, new(2026, 10, 1), new(12, 0), "UTC", _clock);
        _engine.ReconcileStartup();
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
        Assert.Empty(_sink.Missed);
    }

    [Theory]
    [InlineData(3, 29, 1)]
    [InlineData(10, 25, 0)]
    public void Event_uses_existing_dst_gap_and_first_fold_rules(int month, int day, int utcHour)
    {
        var timer = Event() with { OneTime = new() { Date = new(2026, month, day), Time = new(2, 30), TimeZoneId = "Europe/Berlin" } };
        var expected = new DateTimeOffset(2026, month, day, utcHour, 30, 0, TimeSpan.Zero);
        Assert.Equal(expected, OccurrenceSource.Between(timer, expected.AddDays(-1), expected.AddDays(1)).Single());
        _store.Upsert(timer);
        Tick(expected);
        Tick(expected.AddHours(1));
        Assert.Single(_sink.Alerts);
    }

    [Fact]
    public void Event_shows_in_custom_overlay_and_popup_without_duplicate_rows_then_leaves_when_finished()
    {
        var timer = Event();
        var settings = new OverlaySettings { ShowClock = false, ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false };
        var data = new AppData { Timers = [timer] };
        var content = OverlayContent.Build(data, settings, Now.AddMinutes(5));
        Assert.Equal(timer.Id, content.CustomTimers.Single().Id);
        Assert.Empty(content.PopUps);
        Assert.True(content.HasDuePopUp);
        Assert.Single(OverlayContent.Build(data, settings with { ShowCustomTimers = false }, Now.AddMinutes(5)).PopUps);
        var finished = data with { Timers = [timer with { OneTime = timer.OneTime! with { Finished = true } }] };
        Assert.True(OverlayContent.Build(finished, settings, Now.AddMinutes(5)).IsEmpty);
        Assert.Null(UpcomingQuery.OverlayStart(finished, settings, Now.AddMinutes(5)));
    }

    [Fact]
    public void Event_and_weekly_date_fields_round_trip_and_version_6_migration_preserves_timers()
    {
        var weekly = new TimerDef { Kind = TimerKind.Scheduled, Scheduled = Weekly() with { StartDate = new(2026, 10, 2), EndDate = new(2026, 10, 9) } };
        var timer = Event() with { OneTime = Event().OneTime! with { Finished = true } };
        var data = new AppData { DataVersion = 6, Timers = [weekly, timer], Muted = [new(weekly.Id, Now)] };
        var back = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal(timer.OneTime, back.Timers[1].OneTime);
        Assert.Equal(weekly.Scheduled!.StartDate, back.Timers[0].Scheduled!.StartDate);
        Assert.Equal(weekly.Scheduled.EndDate, back.Timers[0].Scheduled!.EndDate);
        var migrated = DataMigrations.Apply(back, new());
        Assert.Same(back.Timers, migrated.Timers);
        Assert.Same(back.Muted, migrated.Muted);
        Assert.Equal(DataMigrations.Current, migrated.DataVersion);
        Assert.True(DataMigrations.Current > 6);
        var old = JsonSerializer.Deserialize<TimerDef>("""{"kind":"Scheduled","scheduled":{"timeZoneId":"UTC","slots":[]}}""", JsonDefaults.Options)!;
        Assert.Null(old.Scheduled!.StartDate);
        Assert.Null(old.Scheduled.EndDate);
        Assert.Null(old.OneTime);
    }

    static ScheduledSpec Weekly() => new() { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Friday, new(12, 0))] };

    [Fact]
    public void Weekly_limits_include_both_boundary_dates_and_stop_after_end()
    {
        var spec = Weekly() with { StartDate = new(2026, 10, 9), EndDate = new(2026, 10, 16) };
        Assert.Equal([Now.AddDays(7), Now.AddDays(14)], ScheduleMath.From(spec, Now).Take(5).ToList());
        Assert.Empty(ScheduleMath.From(spec, Now.AddDays(14).AddSeconds(1)).Take(1).ToList());
        Assert.True(ScheduleMath.IsExpired(spec, Now.AddDays(14).AddSeconds(1)));
        Assert.False(ScheduleMath.IsExpired(spec, Now));
    }

    [Fact]
    public void Weekly_start_only_and_end_only_limits_work_in_timer_zone()
    {
        var spec = Weekly() with { TimeZoneId = "America/Los_Angeles", Slots = [new(DayOfWeek.Friday, new(23, 30))] };
        var last = new DateTimeOffset(2026, 10, 3, 6, 30, 0, TimeSpan.Zero);
        Assert.Equal(last, ScheduleMath.From(spec with { EndDate = new(2026, 10, 2) }, Now).Take(2).ToList().Single());
        Assert.Equal(last.AddDays(7), ScheduleMath.From(spec with { StartDate = new(2026, 10, 9) }, Now).Take(1).ToList().Single());
        Assert.Empty(ScheduleMath.From(spec with { EndDate = new(2026, 10, 2) }, last.AddSeconds(1)).Take(1).ToList());
    }

    [Theory]
    [InlineData(3, 29, 1)]
    [InlineData(10, 25, 0)]
    public void Weekly_single_day_limits_preserve_dst_rules(int month, int day, int utcHour)
    {
        var date = new DateOnly(2026, month, day);
        var spec = Weekly() with { TimeZoneId = "Europe/Berlin", Slots = [new(DayOfWeek.Sunday, new(2, 30))], StartDate = date, EndDate = date };
        var expected = new DateTimeOffset(2026, month, day, utcHour, 30, 0, TimeSpan.Zero);
        Assert.Equal(expected, ScheduleMath.From(spec, expected.AddDays(-1)).Take(2).ToList().Single());
        Assert.Empty(ScheduleMath.From(spec, expected.AddSeconds(1)).Take(1).ToList());
    }

    [Fact]
    public void Weekly_dates_restrict_alerts_and_overlay_and_find_far_future_start()
    {
        var timer = new TimerDef { Kind = TimerKind.Scheduled, Scheduled = Weekly() with { StartDate = new(2026, 11, 6), EndDate = new(2026, 11, 6) }, Alerts = Event().Alerts };
        _store.Upsert(timer);
        Tick(Now);
        Assert.Empty(_sink.Alerts);
        Assert.Empty(UpcomingQuery.ForOverlay(_store.Current, new OverlaySettings(), Now));
        var next = Now.AddDays(35);
        Assert.Equal(next, OccurrenceSource.Next(timer, Now));
        Assert.Equal(next.AddMinutes(-5), UpcomingQuery.OverlayStart(_store.Current, new OverlaySettings(), Now));
        Tick(next);
        Tick(next.AddDays(7));
        Assert.Single(_sink.Alerts);
        Assert.Null(UpcomingQuery.OverlayStart(_store.Current, new OverlaySettings(), next.AddSeconds(1)));
    }

    [Fact]
    public void Invalid_range_is_rejected_without_changing_saved_timer()
    {
        var timer = new TimerDef { Kind = TimerKind.Scheduled, Scheduled = Weekly() };
        _store.Upsert(timer);
        var error = Assert.Throws<ArgumentException>(() => _store.SetWeeklyDateRange(timer.Id, new(2026, 10, 9), new(2026, 10, 2)));
        Assert.Equal("End date must be on or after start date.", error.Message);
        Assert.Null(_store.Current.Timers.Single().Scheduled!.StartDate);
        Assert.Null(_file.Load().Value.Timers.Single().Scheduled!.EndDate);
        Assert.Throws<ArgumentException>(() => _store.Upsert(timer with { Scheduled = Weekly() with { StartDate = new(2026, 10, 9), EndDate = new(2026, 10, 2) } }));
    }

    [Fact]
    public void Bounded_overlay_gate_skips_muted_occurrences_and_has_no_window_after_the_last()
    {
        var timer = new TimerDef { Kind = TimerKind.Scheduled, Scheduled = Weekly() with { EndDate = new(2026, 10, 9) }, Alerts = Event().Alerts };
        var data = new AppData { Timers = [timer], Muted = [new(timer.Id, Now)] };
        Assert.Equal(Now.AddDays(7).AddMinutes(-5), UpcomingQuery.OverlayStart(data, new OverlaySettings(), Now));
        Assert.Null(UpcomingQuery.OverlayStart(data with { Muted = [.. data.Muted, new(timer.Id, Now.AddDays(7))] }, new OverlaySettings(), Now));
        var oneTime = Event();
        Assert.Null(UpcomingQuery.OverlayStart(new() { Timers = [oneTime], Muted = [new(oneTime.Id, Now.AddMinutes(10))] }, new OverlaySettings(), Now));
    }

    [Fact]
    public void Finishing_event_save_failure_leaves_it_pending_and_does_not_issue_notice()
    {
        _store.Upsert(Event());
        _clock.UtcNow = Now.AddMinutes(20);
        Directory.CreateDirectory(_file.FilePath + ".tmp");
        Assert.Throws<StateSaveException>(() => _engine.ReconcileStartup());
        Assert.False(_store.Current.Timers.Single().OneTime!.Finished);
        Assert.False(_file.Load().Value.Timers.Single().OneTime!.Finished);
        Assert.Empty(_sink.Missed);
        Directory.Delete(_file.FilePath + ".tmp");
        _engine.ReconcileStartup();
        Assert.Single(_sink.Missed);
        Assert.True(_file.Load().Value.Timers.Single().OneTime!.Finished);
    }

    [Fact]
    public void Queued_event_alert_survives_completion_but_is_dropped_after_reschedule_or_delete()
    {
        var timer = Event();
        _store.Upsert(timer);
        Tick(Now.AddMinutes(10));
        var alert = _sink.Alerts.Single();
        Assert.NotNull(AlertEligibility.Filter(_store.Current, alert));
        _store.RescheduleEvent(timer.Id, new(2026, 10, 3), new(12, 10), "UTC", _clock);
        Assert.Null(AlertEligibility.Filter(_store.Current, alert));
        _store.Delete(timer.Id);
        Assert.Null(AlertEligibility.Filter(_store.Current, alert));
    }

    [Fact]
    public void Explicit_rearm_after_clock_rollback_can_alert_at_the_same_utc_occurrence()
    {
        var timer = Event();
        _store.Upsert(timer);
        Tick(Now.AddMinutes(10));
        _clock.UtcNow = Now;
        _store.RescheduleEvent(timer.Id, new(2026, 10, 2), new(12, 10), "UTC", _clock);
        Assert.False(_store.Current.Timers.Single().OneTime!.Finished);
        Tick(Now.AddMinutes(10));
        Assert.Equal(2, _sink.Alerts.Count);
        Assert.True(_store.Current.Timers.Single().OneTime!.Finished);
    }

    [Theory]
    [InlineData("2026-10-02", true)]
    [InlineData("2026-02-29", false)]
    [InlineData("2028-02-29", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("02/10/2026", false)]
    public void Date_editor_parsing_is_unambiguous_and_rejects_impossible_dates(string text, bool valid) =>
        Assert.Equal(valid, BdoTimers.Core.Text.Parsing.TryParseDate(text, out _));
}
