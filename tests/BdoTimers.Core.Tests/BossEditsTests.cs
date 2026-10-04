using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public sealed class BossEditsTests : IDisposable
{
    static readonly BossSeed Seed = new("Europe/Berlin",
    [new("Kzarka", [new(DayOfWeek.Monday, "19:00")]), new("Nouver", [new(DayOfWeek.Tuesday, "14:00")])]);

    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(T0);
    readonly JsonFileStore<AppData> _file;
    readonly TimerStore _timers;
    readonly UndoHistory _undo;

    public BossEditsTests()
    {
        _file = new(_dir.File("timers.json"), () => new());
        _timers = new(_file, SeedService.ApplyIfNeeded(new AppData(), Seed, new AlertConfig()));
        _undo = new(_timers, new TodoStore(new(_dir.File("todos.json"), () => new()), new(), _clock), _clock);
    }

    public void Dispose()
    {
        _undo.Dispose();
        _dir.Dispose();
    }

    TimerDef Boss(string name) => _timers.Current.Timers.Single(t => t.IsBuiltIn && t.Name == name);

    [Fact]
    public void An_added_boss_is_a_boss_of_the_selected_region_in_its_server_time()
    {
        var added = _timers.AddBoss();

        Assert.Equal("New boss", added.Name);
        Assert.True(added.IsBuiltIn);
        Assert.True(added.AddedByUser);
        Assert.Equal(BossRegions.Europe, added.BossRegionId);
        Assert.Equal("Europe/Berlin", added.Scheduled!.TimeZoneId);
        Assert.True(BossRegions.IsSelected(_timers.Current, added));
        Assert.Equal("New boss 2", _timers.AddBoss().Name);
        Assert.Equal(2, _file.Load().Value.Timers.Count(t => t.AddedByUser));
    }

    [Fact]
    public void An_added_boss_is_on_the_board_and_the_week_grid()
    {
        var added = _timers.AddBoss();
        _timers.Modify(added.Id, t => t with { Scheduled = t.Scheduled! with { Slots = [new(DayOfWeek.Monday, new(12, 0))] } });

        var now = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
        var board = BossBoard.Build(_timers.Current, now);
        Assert.Equal(added.Id, board.Next!.Bosses.Single().Id);
        Assert.Contains(WeekGrid.Build(_timers.Current, now, TimeZoneInfo.Utc).Rows,
            r => r.Days.SelectMany(d => d).Any(e => e.Boss.Id == added.Id));
    }

    [Fact]
    public void Renaming_needs_a_free_name_and_only_applies_to_added_bosses()
    {
        var added = _timers.AddBoss();

        Assert.False(_timers.RenameBoss(added.Id, " kzarka "));
        Assert.False(_timers.RenameBoss(added.Id, "  "));
        Assert.False(_timers.RenameBoss(Boss("Kzarka").Id, "Garmoth"));
        Assert.True(_timers.RenameBoss(added.Id, " Event Kzarka "));

        Assert.Equal("Event Kzarka", _timers.Current.Timers.Single(t => t.Id == added.Id).Name);
        Assert.NotNull(Boss("Kzarka"));
    }

    [Fact]
    public void A_removed_boss_comes_back_with_undo_unless_its_name_was_taken_again()
    {
        var kzarka = Boss("Kzarka");
        _timers.ToggleMute(kzarka.Id, T0.AddDays(1));

        Assert.True(_undo.DeleteBoss(kzarka.Id));
        Assert.Equal("Boss removed", _undo.Message);
        Assert.DoesNotContain(_timers.Current.Timers, t => t.Id == kzarka.Id);
        Assert.Empty(_timers.Current.Muted);

        Assert.True(_undo.TryUndo());
        Assert.Equal(kzarka, Boss("Kzarka"));
        Assert.Single(_timers.Current.Muted);

        _undo.DeleteBoss(kzarka.Id);
        _timers.Update(d => SeedService.ResetBuiltIns(d, Seed, new AlertConfig()));
        Assert.False(_undo.TryUndo());
        Assert.Single(_timers.Current.Timers, t => t.Name == "Kzarka");
    }

    [Fact]
    public void Restoring_the_timetable_brings_back_removed_bosses_and_keeps_added_ones()
    {
        var nouver = Boss("Nouver");
        var tuned = new AlertConfig { LeadTimesMinutes = [30] };
        _timers.Modify(nouver.Id, t => t with { Alerts = tuned, ImageFile = "nouver.png",
            Scheduled = t.Scheduled! with { Slots = [new(DayOfWeek.Friday, new(1, 0))] } });
        _undo.DeleteBoss(Boss("Kzarka").Id);
        var added = _timers.AddBoss();

        _timers.Update(d => SeedService.ResetBuiltIns(d, Seed, new AlertConfig()));

        var restored = Boss("Nouver");
        Assert.Equal(nouver.Id, restored.Id);
        Assert.Equal(tuned, restored.Alerts);
        Assert.Equal("nouver.png", restored.ImageFile);
        Assert.Equal([new Slot(DayOfWeek.Tuesday, new(14, 0))], restored.Scheduled!.Slots);
        Assert.False(Boss("Kzarka").AddedByUser);
        Assert.Equal(added, _timers.Current.Timers.Single(t => t.Id == added.Id));
        Assert.Equal(3, _timers.Current.Timers.Count(t => t.IsBuiltIn));
    }

    [Fact]
    public void Restoring_the_timetable_adopts_an_added_boss_under_a_bundled_name()
    {
        var kzarka = Boss("Kzarka");
        _undo.DeleteBoss(kzarka.Id);
        var added = _timers.AddBoss();
        Assert.True(_timers.RenameBoss(added.Id, "Kzarka"));
        _timers.SetEnabled(added.Id, false);

        _timers.Update(d => SeedService.ResetBuiltIns(d, Seed, new AlertConfig()));

        var adopted = Boss("Kzarka");
        Assert.Equal(added.Id, adopted.Id);
        Assert.False(adopted.AddedByUser);
        Assert.False(adopted.Enabled);
        Assert.Equal([new Slot(DayOfWeek.Monday, new(19, 0))], adopted.Scheduled!.Slots);
    }

    [Fact]
    public void A_timetable_update_leaves_added_bosses_and_asks_before_bringing_back_a_removed_one()
    {
        var added = _timers.AddBoss();
        _undo.DeleteBoss(Boss("Nouver").Id);
        BossSeed next = new("Europe/Berlin",
        [new("Kzarka", [new(DayOfWeek.Monday, "20:00")]), new("Nouver", [new(DayOfWeek.Tuesday, "15:00")])]);

        var review = TimetableUpdates.Review(_timers.Current, next);

        Assert.Equal(new[] { "Kzarka", "Nouver" }, review.Changes.Select(c => c.Name));
        Assert.False(review.Changes.Single(c => c.Name == "Kzarka").HasCustomTimes);
        var nouver = review.Changes.Single(c => c.Name == "Nouver");
        Assert.True(nouver.HasCustomTimes);
        Assert.Null(nouver.Current);
        Assert.False(nouver.AddedByUser);

        var applied = TimetableUpdates.Apply(_timers.Current, next, ["Kzarka"]);
        Assert.Contains(applied.Timers, t => t.Id == added.Id);
        Assert.DoesNotContain(applied.Timers, t => t.Name == "Nouver");
    }

    [Fact]
    public void A_timetable_boss_with_an_added_boss_s_name_takes_it_over_only_when_chosen()
    {
        var added = _timers.AddBoss();
        _timers.RenameBoss(added.Id, "Vell");
        BossSeed next = new("Europe/Berlin", [.. Seed.Bosses, new("Vell", [new(DayOfWeek.Sunday, "16:00")])]);

        var vell = TimetableUpdates.Review(_timers.Current, next).Changes.Single();
        Assert.Equal("Vell", vell.Name);
        Assert.True(vell.HasCustomTimes);
        Assert.True(vell.AddedByUser);

        var kept = TimetableUpdates.Apply(_timers.Current, next, []);
        Assert.True(kept.Timers.Single(t => t.Name == "Vell").AddedByUser);

        var applied = TimetableUpdates.Apply(_timers.Current, next, ["Vell"]);
        var taken = applied.Timers.Single(t => t.Name == "Vell");
        Assert.Equal(added.Id, taken.Id);
        Assert.False(taken.AddedByUser);
        Assert.Equal([new Slot(DayOfWeek.Sunday, new(16, 0))], taken.Scheduled!.Slots);
    }

    [Fact]
    public void Added_bosses_do_not_block_the_baseline_of_an_unedited_timetable()
    {
        var data = _timers.Current;
        data = BossRegions.WithState(data, BossRegions.State(data) with { AcceptedBossTimetable = null });
        data = data with { Timers = [.. data.Timers, BossEdits.Create(data)] };

        var initialized = TimetableUpdates.InitializeBaseline(data, Seed);

        Assert.NotNull(BossRegions.State(initialized).AcceptedBossTimetable);
        Assert.False(TimetableUpdates.Review(initialized, Seed).NeedsReview);
    }

    [Fact]
    public void Saved_data_rejects_an_added_flag_on_a_timer_that_is_not_a_boss()
    {
        const string timer = """{ "timers": [ { "id": "6f1c2a8e-0d9b-4a77-9a51-1f5a0e6f2b11", "name": "x", "kind": "Countdown", "countdown": {}FLAG } ] }""";
        File.WriteAllText(_file.FilePath, timer.Replace("FLAG", ""));
        Assert.Null(_file.Load().RecoveredBackupPath);

        File.WriteAllText(_file.FilePath, timer.Replace("FLAG", """, "addedByUser": true"""));
        Assert.NotNull(_file.Load().RecoveredBackupPath);
    }
}
