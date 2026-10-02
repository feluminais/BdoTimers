using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class BossRegionsTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);

    // The same name intentionally exists twice: region identity must precede name matching.
    static AppData Both(string selected = "na") => JsonSerializer.Deserialize<AppData>($$$"""
        {
          "selectedBossRegion": "{{{selected}}}",
          "seedApplied": true,
          "timers": [
            { "id":"10000000-0000-0000-0000-000000000001", "name":"Kzarka", "kind":"Scheduled",
              "isBuiltIn":true, "bossRegionId":"eu", "enabled":false,
              "scheduled":{"timeZoneId":"UTC","slots":[{"day":"Monday","time":"12:00:00"}]} },
            { "id":"20000000-0000-0000-0000-000000000001", "name":"Kzarka", "kind":"Scheduled",
              "isBuiltIn":true, "bossRegionId":"na", "enabled":true, "alerts":{"overlay":{"enabled":true}},
              "scheduled":{"timeZoneId":"UTC","slots":[{"day":"Monday","time":"13:00:00"}]} }
          ]
        }
        """, JsonDefaults.Options)!;

    [Fact]
    public void Board_and_week_grid_include_only_selected_region()
    {
        var data = Both("eu");
        var grid = WeekGrid.Build(data, Now, TimeZoneInfo.Utc);
        Assert.All(grid.Rows.SelectMany(r => r.Days).SelectMany(d => d), e =>
            Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), e.Boss.Id));
        Assert.Null(BossBoard.Build(data, Now).Next); // EU's only boss is disabled.
    }

    [Fact]
    public void Inactive_bosses_do_not_trigger_overlay_popups()
    {
        var data = Both("eu");
        var at = Now.AddHours(2).AddMinutes(-1);
        Assert.Empty(UpcomingQuery.ForOverlay(data, new OverlaySettings(), at));
        Assert.False(new OverlayPopUpGate().MayBeDue(data, new OverlaySettings(), at));
        var overlay = OverlayContent.Build(data, new OverlaySettings(), at);
        Assert.Null(overlay.Next);
        Assert.Empty(overlay.PopUps);
    }

    [Fact]
    public void Boss_alert_reset_leaves_inactive_region_choices_alone()
    {
        using var dir = new TempDir();
        var data = Both();
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), data);
        store.ResetBossAlerts();
        Assert.False(store.Current.Timers[0].Enabled);
        Assert.True(store.Current.Timers[1].Enabled);
    }

    [Fact]
    public void Timetable_comparison_uses_selected_region_even_when_names_match()
    {
        var seed = new BossSeed("UTC", [new("Kzarka", [new(DayOfWeek.Monday, "13:00")])]);
        var review = TimetableUpdates.Review(Both(), seed);
        Assert.Empty(review.Changes);
    }

    [Fact]
    public void Legacy_migration_preserves_ids_edits_choices_mutes_and_baseline_as_eu()
    {
        var seed = SeedService.LoadEmbedded();
        var boss = SeedService.ToTimers(seed, new())[0] with
        {
            BossRegionId = null, Enabled = false,
            Scheduled = new() { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Friday, new(9, 35))] },
            Alerts = new() { LeadTimesMinutes = [17], Sound = new() { Key = "ping" } },
        };
        var custom = TestTimers.Scheduled("My timer", DayOfWeek.Friday, 9, 15, 1);
        var legacy = new AppData
        {
            DataVersion = 5, SeedApplied = true, AcceptedBossTimetable = seed,
            Timers = [boss, custom], Muted = [new(boss.Id, Now)],
        };
        var migrated = DataMigrations.Apply(legacy, new() { TimetableNoticeRevision = "old-notice" });
        Assert.Equal("eu", migrated.SelectedBossRegion);
        Assert.Equal(boss with { BossRegionId = "eu" }, migrated.Timers[0]);
        Assert.Same(custom, migrated.Timers[1]);
        Assert.Equal(legacy.Muted, migrated.Muted);
        var eu = Assert.Single(migrated.BossRegions);
        Assert.True(eu.SeedApplied);
        Assert.Same(seed, eu.AcceptedBossTimetable);
        Assert.Equal("old-notice", eu.TimetableNoticeRevision);
        Assert.Same(migrated, DataMigrations.Apply(migrated, new()));
        Assert.Same(migrated, SeedService.ApplyIfNeeded(migrated, seed, new()));
    }

    [Fact]
    public void Scheduler_and_speech_use_selected_bosses_and_custom_timers()
    {
        using var dir = new TempDir();
        var at = Now.AddHours(2).AddMinutes(-5);
        var custom = TestTimers.Scheduled("Custom", DayOfWeek.Monday, 13, 0, 5, 0) with
            { Scheduled = new() { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Monday, new(13, 0))] } };
        var data = Both() with { Timers = [.. Both().Timers.Select(t => t with
        {
            Enabled = true, Scheduled = custom.Scheduled,
            Alerts = new() { LeadTimesMinutes = [5, 0] },
        }), custom] };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), data);
        var settings = new PersistentState<AppSettings>(new(dir.File("settings.json"), () => new()), new());
        var clock = new FakeClock(at.AddSeconds(-30));
        var sink = new Sink();
        var engine = new SchedulerEngine(store, settings, sink, clock);
        engine.Tick();
        Assert.Equal(["Custom and Kzarka in 5 minutes", "Custom and Kzarka now"], Assert.Single(sink.Speech));
        clock.UtcNow = at;
        engine.Tick();
        var alert = Assert.Single(sink.Alerts);
        Assert.Equal([custom.Id, data.Timers[1].Id], alert.Timers.Select(t => t.Id));
    }

    [Fact]
    public void Selection_boundary_suppresses_already_due_leads_but_future_leads_still_fire()
    {
        using var dir = new TempDir();
        var boundary = Now.AddHours(2).AddMinutes(-2);
        var data = Both() with
        {
            BossAlertsAfterUtc = boundary,
            Timers = Both().Timers.Select(t => t with { Alerts = new() { LeadTimesMinutes = [5, 1, 0] } }).ToList(),
        };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), data);
        var settings = new PersistentState<AppSettings>(new(dir.File("settings.json"), () => new()), new());
        var clock = new FakeClock(boundary);
        var sink = new Sink();
        var engine = new SchedulerEngine(store, settings, sink, clock);
        engine.Tick();
        Assert.Empty(sink.Alerts);
        clock.UtcNow = boundary.AddMinutes(-3); // Clock rollback must not revive the old lead.
        engine.Tick();
        Assert.Empty(sink.Alerts);
        clock.UtcNow = boundary.AddMinutes(1);
        engine.Tick();
        clock.UtcNow = boundary.AddMinutes(2);
        engine.Tick();
        Assert.Equal([1, 0], sink.Alerts.Select(a => a.LeadMinutes));
    }

    [Fact]
    public void Switching_and_restarting_restore_each_regions_personal_configuration_without_duplicates()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var initial = SeedService.ApplyIfNeeded(DataMigrations.Apply(new(), new()), SeedService.LoadEmbedded(), new());
        var store = new TimerStore(file, initial);
        var clock = new FakeClock(Now);
        var euBoss = initial.Timers.First(t => t.Name == "Kzarka");
        store.Modify(euBoss.Id, t => t with
        {
            Enabled = false, Alerts = new() { LeadTimesMinutes = [27], Tts = new() { Template = "My EU line" } },
            Scheduled = new() { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Friday, new(5, 35))] },
        });
        store.ToggleMute(euBoss.Id, Now.AddDays(4));
        var editedEu = store.Current.Timers.Single(t => t.Id == euBoss.Id);
        var newerEu = SeedService.LoadEmbedded() with { Bosses = [new("Kzarka", [new(DayOfWeek.Friday, "06:00")])] };
        store.Update(d => TimetableUpdates.Apply(d, newerEu, []));
        store.Update(d => BossRegions.WithState(d, BossRegions.State(d) with { TimetableNoticeRevision = "eu-notice" }));

        store.SelectBossRegion("na", clock);
        Assert.Equal("na", store.Current.SelectedBossRegion);
        var naBoss = store.Current.Timers.First(t => t.Name == "Kzarka" && t.BossRegionId == "na");
        store.Modify(naBoss.Id, t => t with
        {
            Enabled = false, Alerts = new() { LeadTimesMinutes = [32], Sound = new() { Key = "ping" } },
            Scheduled = new() { TimeZoneId = "America/Los_Angeles", Slots = [new(DayOfWeek.Saturday, new(8, 10))] },
        });
        var editedNa = store.Current.Timers.Single(t => t.Id == naBoss.Id);
        store.Update(d => BossRegions.WithState(d, BossRegions.State(d) with { TimetableNoticeRevision = "na-notice" }));
        clock.UtcNow = Now.AddSeconds(1);
        store.SelectBossRegion("eu", clock);
        Assert.Equal(editedEu, store.Current.Timers.Single(t => t.Id == euBoss.Id));
        Assert.False(TimetableUpdates.Review(store.Current, newerEu).NeedsReview);
        Assert.Equal("eu-notice", BossRegions.State(store.Current).TimetableNoticeRevision);
        Assert.Contains(new MutedOccurrence(euBoss.Id, Now.AddDays(4)), store.Current.Muted);
        var count = store.Current.Timers.Count;

        var loaded = new TimerStore(file, file.Load().Value);
        loaded.SelectBossRegion("na", clock);
        var restoredNa = loaded.Current.Timers.Single(t => t.Id == naBoss.Id);
        Assert.False(restoredNa.Enabled);
        Assert.Equal([32], restoredNa.Alerts.LeadTimesMinutes);
        Assert.Equal("ping", restoredNa.Alerts.Sound.Key);
        Assert.True(TimetableUpdates.Same(editedNa.Scheduled, restoredNa.Scheduled));
        Assert.Equal("na-notice", BossRegions.State(loaded.Current).TimetableNoticeRevision);
        Assert.False(TimetableUpdates.Review(loaded.Current, SeedService.LoadEmbedded("na")).NeedsReview);
        loaded.SelectBossRegion("eu", clock);
        loaded.SelectBossRegion("na", clock);
        Assert.Equal(count, loaded.Current.Timers.Count);
        Assert.Equal(26, loaded.Current.Timers.Count(t => t.IsBuiltIn));
        Assert.Equal(count, loaded.Current.Timers.Select(t => t.Id).Distinct().Count());
        var unchanged = loaded.Current;
        loaded.SelectBossRegion("na", clock);
        Assert.Same(unchanged, loaded.Current);
    }

    [Fact]
    public void Timetable_apply_and_reset_do_not_touch_inactive_regions()
    {
        var data = Both();
        var eu = data.Timers[0];
        var seed = new BossSeed("UTC", [new("Kzarka", [new(DayOfWeek.Monday, "14:00")]), new("Vell", [new(DayOfWeek.Friday, "15:00")])]);
        var changed = TimetableUpdates.Apply(data, seed, ["Kzarka", "Vell"]);
        Assert.Same(eu, changed.Timers.Single(t => t.Id == eu.Id));
        Assert.Equal(data.Timers[1].Id, changed.Timers.Single(t => t.Name == "Kzarka" && t.BossRegionId == "na").Id);
        Assert.Equal("na", changed.Timers.Single(t => t.Name == "Vell").BossRegionId);
        var reset = SeedService.ResetBuiltIns(changed, new("UTC", []), new());
        Assert.Same(eu, Assert.Single(reset.Timers));
        Assert.True(BossRegions.State(reset, "na").SeedApplied);
        Assert.Null(BossRegions.State(reset, "eu").AcceptedBossTimetable);
    }

    [Fact]
    public void Rapid_switches_suppress_past_leads_without_discarding_custom_alerts()
    {
        using var dir = new TempDir();
        var boundary = Now.AddHours(2).AddMinutes(-2);
        var data = Both() with
        {
            BossRegions = [new() { RegionId = "eu", SeedApplied = true }, new() { RegionId = "na", SeedApplied = true }],
            Timers = Both().Timers.Select(t => t with { Alerts = new() { LeadTimesMinutes = [5, 0] } }).ToList(),
        };
        var custom = TestTimers.Countdown(boundary, 0);
        data = data with { Timers = [.. data.Timers, custom] };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), data);
        var settings = new PersistentState<AppSettings>(new(dir.File("settings.json"), () => new()), new());
        var clock = new FakeClock(boundary);
        var sink = new Sink();
        var engine = new SchedulerEngine(store, settings, sink, clock);
        store.SelectBossRegion("eu", clock);
        store.SelectBossRegion("na", clock); // Neither selection was observed by a scheduler tick.
        engine.Tick();
        Assert.Equal(custom.Id, Assert.Single(Assert.Single(sink.Alerts).Timers).Id);
        Assert.Equal(boundary, store.Current.BossAlertsAfterUtc);
        Assert.NotEqual(Guid.Empty, store.Current.BossSelectionVersion);
    }

    [Fact]
    public void Queued_alerts_cannot_survive_switching_back_but_their_custom_timers_still_alert()
    {
        var data = Both();
        var occurrence = Now.AddHours(2);
        var custom = TestTimers.Countdown(occurrence, 0);
        data = data with { Timers = [.. data.Timers, custom] };
        var queued = new AlertEvent([data.Timers[1], custom], occurrence, 0, 0) { BossSelectionVersion = data.BossSelectionVersion };
        Assert.Same(queued, AlertEligibility.Filter(data, queued));
        var switchedBack = data with { BossSelectionVersion = Guid.NewGuid() };
        Assert.Equal(custom.Id, Assert.Single(AlertEligibility.Filter(switchedBack, queued)!.Timers).Id);
        Assert.Null(AlertEligibility.Filter(switchedBack, queued with { Timers = [data.Timers[1]] }));
    }

    [Fact]
    public void Switching_leaves_custom_schedules_and_todo_settings_and_reset_boundaries_unchanged()
    {
        using var dir = new TempDir();
        var clock = new FakeClock(Now);
        var settings = new AppSettings
        {
            DailyTodoReset = new() { Hour = 9, Minute = 17, LocalTime = true },
            WeeklyTodoReset = new() { Day = DayOfWeek.Friday, Hour = 21, Minute = 30 },
        };
        var todos = new TodoStore(new(dir.File("todos.json"), () => new()), TodoSeed.Create(Now, settings), clock);
        var personalId = todos.CreateList(TodoCadence.Daily, settings);
        todos.Modify(personalId, l => l with { Rows = [new() { Text = "Personal", Done = true }] });
        var before = todos.Current;
        var custom = TestTimers.Scheduled("Personal schedule", DayOfWeek.Friday, 21, 30, 1);
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [custom] });
        store.SelectBossRegion("na", clock);
        store.SelectBossRegion("eu", clock);
        todos.Reconcile(settings);
        Assert.Same(before, todos.Current);
        Assert.Same(custom, store.Current.Timers.Single(t => t.Id == custom.Id));
    }

    [Fact]
    public void Failed_region_save_keeps_selection_and_bosses_atomic()
    {
        using var dir = new TempDir();
        var path = Directory.CreateDirectory(dir.File("not-a-file")).FullName;
        var data = Both("eu");
        var store = new TimerStore(new(path, () => new()), data);
        Assert.Throws<StateSaveException>(() => store.SelectBossRegion("na", new FakeClock(Now)));
        Assert.Same(data, store.Current);
        Assert.Throws<ArgumentException>(() => store.SelectBossRegion("unknown", new FakeClock(Now)));
    }

    [Fact]
    public void Region_switch_does_not_replay_an_old_overlay_window_and_custom_popups_remain()
    {
        var at = Now.AddHours(2).AddMinutes(-2);
        var custom = TestTimers.Countdown(at.AddMinutes(1), 0) with
            { Alerts = new() { Overlay = new() { Enabled = true, ShowMinutesBefore = 5 } } };
        var data = Both() with { BossAlertsAfterUtc = at, Timers = [.. Both().Timers, custom] };
        Assert.Equal(custom.Id, Assert.Single(UpcomingQuery.ForOverlay(data, new OverlaySettings(), at)).Timer.Id);
        var bossesOnly = data with { Timers = data.Timers.Where(t => t.IsBuiltIn).ToList() };
        Assert.False(new OverlayPopUpGate().MayBeDue(bossesOnly, new OverlaySettings(), at));
        // Pinning the overlay still shows the selected region's next spawn in its board section.
        Assert.Equal(data.Timers[1].Id, Assert.Single(OverlayContent.Build(data, new() { ShowNext = true }, at).Next!.Bosses).Id);
    }

    [Theory]
    [InlineData("na", "America/Los_Angeles", 2, "2026-03-08T10:30:00Z")]
    [InlineData("na", "America/Los_Angeles", 1, "2026-11-01T08:30:00Z")]
    [InlineData("eu", "Europe/Berlin", 2, "2026-03-29T01:30:00Z")]
    [InlineData("eu", "Europe/Berlin", 2, "2026-10-25T00:30:00Z")]
    public void Edited_boss_times_alert_once_through_each_regions_dst_gap_or_fold(
        string region, string zone, int hour, string expected)
    {
        using var dir = new TempDir();
        var at = DateTimeOffset.Parse(expected);
        var clock = new FakeClock(at.AddMinutes(-5));
        var boss = new TimerDef
        {
            Name = "Edited boss", Kind = TimerKind.Scheduled, IsBuiltIn = true, BossRegionId = region,
            Scheduled = new() { TimeZoneId = zone, Slots = [new(DayOfWeek.Sunday, new(hour, 30))] },
            Alerts = new() { LeadTimesMinutes = [0] },
        };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new()
            { SelectedBossRegion = region, Timers = [boss] });
        var settings = new PersistentState<AppSettings>(new(dir.File("settings.json"), () => new()), new());
        var sink = new Sink();
        var engine = new SchedulerEngine(store, settings, sink, clock);
        engine.Tick();
        Assert.Empty(sink.Alerts);
        Assert.Equal(at, BossBoard.Build(store.Current, clock.UtcNow).Next!.AtUtc);
        clock.UtcNow = at;
        engine.Tick();
        engine.Tick();
        clock.UtcNow = at.AddHours(1); // The repeated autumn wall-clock hour must not create a second alert.
        engine.Tick();
        Assert.Equal(at, Assert.Single(sink.Alerts).OccurrenceUtc);
    }

    sealed class Sink : IAlertSink
    {
        public List<AlertEvent> Alerts { get; } = [];
        public List<IReadOnlyCollection<string>> Speech { get; } = [];
        public void Dispatch(AlertEvent alert) => Alerts.Add(alert);
        public void PrepareSpeech(IReadOnlyCollection<string> texts) => Speech.Add(texts);
        public void NotifyEndedWhileAway(TimerDef timer) { }
    }
}
