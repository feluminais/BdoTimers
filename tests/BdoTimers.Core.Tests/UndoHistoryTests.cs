using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public sealed class UndoHistoryTests : IDisposable
{
    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(T0);
    readonly TimerStore _timers;
    readonly TodoStore _todos;
    readonly JsonFileStore<AppData> _timerFile;
    readonly JsonFileStore<TodoData> _todoFile;

    public UndoHistoryTests()
    {
        _timerFile = new(_dir.File("timers.json"), () => new());
        _todoFile = new(_dir.File("todos.json"), () => new());
        _timers = new(_timerFile, new());
        _todos = new(_todoFile, new(), _clock);
    }

    public void Dispose() => _dir.Dispose();

    static TimerDef Countdown(string name = "Timer") => new()
    {
        Name = name, Kind = TimerKind.Countdown,
        Countdown = new() { Duration = TimeSpan.FromMinutes(30) },
    };

    [Fact]
    public void Deleted_timer_undo_restores_record_order_mutes_and_original_deadline()
    {
        var first = Countdown("First");
        var deleted = Countdown("Deleted") with
        {
            ImageFile = "picture.jpg",
            Countdown = new() { Duration = TimeSpan.FromMinutes(30), Status = CountdownStatus.Running, EndsAtUtc = T0.AddMinutes(30) },
        };
        var last = Countdown("Last");
        _timers.Update(d => d with { Timers = [first, deleted, last], Muted = [new(deleted.Id, T0.AddMinutes(30))] });
        var undo = new UndoHistory(_timers, _todos, _clock);

        Assert.True(undo.DeleteTimer(deleted.Id));
        _clock.UtcNow = T0.AddSeconds(5);
        Assert.True(undo.TryUndo());

        Assert.Equal(new[] { first.Id, deleted.Id, last.Id }, _timers.Current.Timers.Select(t => t.Id));
        Assert.Same(deleted, _timers.Current.Timers[1]);
        Assert.Equal(T0.AddMinutes(30), _timers.Current.Timers[1].Countdown!.EndsAtUtc);
        Assert.Equal(new MutedOccurrence(deleted.Id, T0.AddMinutes(30)), Assert.Single(_timers.Current.Muted));
        Assert.Equal(deleted.Id, _timerFile.Load().Value.Timers[1].Id);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Reset_undo_restores_original_running_deadline_and_preserves_later_name_edit()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        _timers.Start(timer.Id, T0);
        var original = _timers.Current.Timers[0].Countdown;
        var undo = new UndoHistory(_timers, _todos, _clock);

        Assert.True(undo.ResetTimer(timer.Id));
        _timers.Modify(timer.Id, t => t with { Name = "Renamed" });
        _clock.UtcNow = T0.AddSeconds(8);
        Assert.True(undo.TryUndo());

        var restored = Assert.Single(_timers.Current.Timers);
        Assert.Same(original, restored.Countdown);
        Assert.Equal("Renamed", restored.Name);
        Assert.Equal(T0.AddMinutes(30), restored.Countdown!.EndsAtUtc);
        Assert.Equal(T0.AddMinutes(30), _timerFile.Load().Value.Timers[0].Countdown!.EndsAtUtc);
    }

    [Fact]
    public void Reset_undo_does_not_replace_a_new_run()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        _timers.Start(timer.Id, T0);
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.ResetTimer(timer.Id);
        _timers.Start(timer.Id, T0.AddSeconds(2));

        Assert.False(undo.TryUndo());

        Assert.Equal(T0.AddMinutes(30).AddSeconds(2), _timers.Current.Timers[0].Countdown!.EndsAtUtc);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Horse_run_reset_undo_restores_removed_run()
    {
        _timers.Upsert(Presets.CreateHorseRegistration());
        _timers.StartHorseRegistration(T0);
        var run = _timers.Current.Timers.Single(t => t.Preset == Presets.HorseRegistrationRun);
        var undo = new UndoHistory(_timers, _todos, _clock);

        Assert.True(undo.ResetTimer(run.Id));
        Assert.DoesNotContain(_timers.Current.Timers, t => t.Id == run.Id);
        Assert.True(undo.TryUndo());

        Assert.Same(run, _timers.Current.Timers.Single(t => t.Id == run.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deleted_list_undo_restores_checks_original_boundary_and_order(bool builtIn)
    {
        var first = new TodoList { Name = "First" };
        var deleted = new TodoList
        {
            Name = "Deleted", IsBuiltIn = builtIn, NextResetUtc = T0.AddDays(1),
            Rows = [new() { Text = "Task", Done = true }],
        };
        var last = new TodoList { Name = "Last" };
        _todos.Update(d => d with { Lists = [first, deleted, last] });
        var undo = new UndoHistory(_timers, _todos, _clock);

        Assert.True(undo.DeleteTodoList(deleted.Id));
        Assert.True(undo.TryUndo());

        Assert.Equal(new[] { first.Id, deleted.Id, last.Id }, _todos.Current.Lists.Select(l => l.Id));
        Assert.Same(deleted, _todos.Current.Lists[1]);
        Assert.True(_todoFile.Load().Value.Lists[1].Rows[0].Done);
        Assert.Equal(T0.AddDays(1), _todoFile.Load().Value.Lists[1].NextResetUtc);
    }

    [Fact]
    public void Stale_ids_and_idle_resets_do_not_save_or_create_history()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock);
        var changes = 0;
        _timers.Changed += () => changes++;
        _todos.Changed += () => changes++;

        Assert.False(undo.DeleteTimer(Guid.NewGuid()));
        Assert.False(undo.DeleteTodoList(Guid.NewGuid()));
        Assert.False(undo.ResetTimer(Guid.NewGuid()));
        Assert.False(undo.ResetTimer(timer.Id));

        Assert.False(undo.CanUndo);
        Assert.Equal(0, changes);
        Assert.False(File.Exists(_todoFile.FilePath));
    }

    [Fact]
    public void Deleted_timer_undo_does_not_overwrite_an_existing_id()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.DeleteTimer(timer.Id);
        _timers.Upsert(timer with { Name = "Replacement" });

        Assert.False(undo.TryUndo());

        Assert.Equal("Replacement", Assert.Single(_timers.Current.Timers).Name);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Failed_delete_does_not_create_history_or_change_state()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock);
        File.Delete(_timerFile.FilePath);
        Directory.CreateDirectory(_timerFile.FilePath);

        Assert.Throws<StateSaveException>(() => undo.DeleteTimer(timer.Id));

        Assert.Same(timer, Assert.Single(_timers.Current.Timers));
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Failed_undo_keeps_the_entry_for_retry()
    {
        var timer = Countdown();
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.DeleteTimer(timer.Id);
        File.Delete(_timerFile.FilePath);
        Directory.CreateDirectory(_timerFile.FilePath);

        Assert.Throws<StateSaveException>(() => undo.TryUndo());
        Assert.Empty(_timers.Current.Timers);
        Assert.True(undo.CanUndo);

        Directory.Delete(_timerFile.FilePath);
        Assert.True(undo.TryUndo());
        Assert.Same(timer, Assert.Single(_timers.Current.Timers));
    }

    [Fact]
    public void Expiration_keeps_deleted_picture_available_until_undo_deadline()
    {
        var picture = _dir.File("picture.jpg");
        File.WriteAllText(picture, "image");
        var timer = Countdown() with { ImageFile = "picture.jpg" };
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock, image => File.Delete(_dir.File(image)));
        undo.DeleteTimer(timer.Id);

        _clock.UtcNow = T0.AddSeconds(14);
        undo.Refresh();
        Assert.True(File.Exists(picture));
        Assert.True(undo.CanUndo);
        _clock.UtcNow = T0.AddSeconds(15);
        undo.Refresh();

        Assert.False(File.Exists(picture));
        Assert.False(undo.CanUndo);
        Assert.False(undo.TryUndo());
    }

    [Fact]
    public void Capacity_evicts_oldest_entry_and_history_unwinds_in_reverse_order()
    {
        var first = Countdown("First");
        var second = Countdown("Second");
        var third = Countdown("Third");
        _timers.Update(d => d with { Timers = [first, second, third] });
        var undo = new UndoHistory(_timers, _todos, _clock, capacity: 2);
        undo.DeleteTimer(first.Id);
        undo.DeleteTimer(second.Id);
        undo.DeleteTimer(third.Id);

        Assert.True(undo.TryUndo());
        Assert.True(undo.TryUndo());
        Assert.False(undo.TryUndo());

        Assert.Equal(new[] { second.Id, third.Id }, _timers.Current.Timers.Select(t => t.Id));
    }

    [Fact]
    public void Shared_picture_is_not_released_while_another_delete_can_be_undone()
    {
        var releases = new List<string>();
        var first = Countdown("First") with { ImageFile = "picture.jpg" };
        var second = Countdown("Second") with { ImageFile = "picture.jpg" };
        _timers.Update(d => d with { Timers = [first, second] });
        var undo = new UndoHistory(_timers, _todos, _clock, releases.Add);
        undo.DeleteTimer(first.Id);
        undo.DeleteTimer(second.Id);

        undo.Dismiss();
        Assert.Empty(releases);
        undo.Dismiss();

        Assert.Equal(new[] { "picture.jpg" }, releases);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Undo_keeps_the_restored_picture_after_history_cleanup()
    {
        var picture = _dir.File("picture.jpg");
        File.WriteAllText(picture, "image");
        var timer = Countdown() with { ImageFile = "picture.jpg" };
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock, image => File.Delete(_dir.File(image)));
        undo.DeleteTimer(timer.Id);

        Assert.True(undo.TryUndo());
        _clock.UtcNow = T0.AddMinutes(1);
        undo.Refresh();
        undo.Dispose();

        Assert.True(File.Exists(picture));
        Assert.Equal("picture.jpg", Assert.Single(_timers.Current.Timers).ImageFile);
    }

    [Fact]
    public void Picture_replacement_cannot_release_a_picture_retained_by_a_pending_delete()
    {
        var picture = _dir.File("shared.jpg");
        File.WriteAllText(picture, "image");
        var first = Countdown("First") with { ImageFile = "shared.jpg" };
        var second = Countdown("Second") with { ImageFile = "shared.jpg" };
        _timers.Update(d => d with { Timers = [first, second] });
        var undo = new UndoHistory(_timers, _todos, _clock, image => File.Delete(_dir.File(image)));
        undo.DeleteTimer(first.Id);
        _timers.Modify(second.Id, t => t with { ImageFile = null });

        undo.ReleasePicture("shared.jpg");

        Assert.True(File.Exists(picture));
        undo.Dismiss();
        Assert.False(File.Exists(picture));
    }

    [Fact]
    public void Stopwatch_reset_undo_restores_the_original_start()
    {
        var timer = new TimerDef
        {
            Name = "Stopwatch", Kind = TimerKind.Stopwatch,
            Stopwatch = new() { Status = CountdownStatus.Running, StartedAtUtc = T0.AddMinutes(-12) },
        };
        _timers.Upsert(timer);
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.ResetTimer(timer.Id);
        _clock.UtcNow = T0.AddSeconds(5);

        Assert.True(undo.TryUndo());

        Assert.Same(timer.Stopwatch, Assert.Single(_timers.Current.Timers).Stopwatch);
        Assert.Equal(TimeSpan.FromMinutes(12).Add(TimeSpan.FromSeconds(5)), StopwatchOps.Elapsed(timer.Stopwatch!, _clock.UtcNow));
    }

    [Fact]
    public void Deleted_timer_undo_preserves_pending_completion_snapshot()
    {
        var timer = Countdown();
        var completed = timer with
        {
            Countdown = timer.Countdown! with { Status = CountdownStatus.Running, EndsAtUtc = T0.AddSeconds(-1) },
        };
        _timers.Update(d => d with { Timers = [timer], CompletedCountdowns = [completed] });
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.DeleteTimer(timer.Id);
        Assert.Empty(_timers.Current.CompletedCountdowns);

        Assert.True(undo.TryUndo());

        Assert.Same(completed, Assert.Single(_timers.Current.CompletedCountdowns));
    }

    [Fact]
    public void Idle_timer_reset_cancels_a_pending_completion_and_undo_restores_it()
    {
        var timer = Countdown();
        var completed = timer with
        {
            Countdown = timer.Countdown! with { Status = CountdownStatus.Running, EndsAtUtc = T0.AddSeconds(-1) },
        };
        _timers.Update(d => d with { Timers = [timer], CompletedCountdowns = [completed] });
        var undo = new UndoHistory(_timers, _todos, _clock);

        Assert.True(undo.ResetTimer(timer.Id));
        Assert.Empty(_timers.Current.CompletedCountdowns);
        Assert.True(undo.TryUndo());

        Assert.Same(completed, Assert.Single(_timers.Current.CompletedCountdowns));
    }

    [Fact]
    public void Horse_run_undo_does_not_reuse_a_number_taken_by_a_new_run()
    {
        _timers.Upsert(Presets.CreateHorseRegistration());
        _timers.StartHorseRegistration(T0);
        var old = _timers.Current.Timers.Single(Presets.IsActiveHorseRun);
        var undo = new UndoHistory(_timers, _todos, _clock);
        undo.ResetTimer(old.Id);
        _timers.StartHorseRegistration(T0.AddSeconds(1));

        Assert.False(undo.TryUndo());

        Assert.DoesNotContain(_timers.Current.Timers, t => t.Id == old.Id);
        Assert.Single(_timers.Current.Timers, Presets.IsActiveHorseRun);
    }
}
