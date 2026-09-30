using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class TimerStoreTests
{
    static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    static (TimerStore Store, JsonFileStore<AppData> File) NewStore(TempDir dir)
    {
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        return (new TimerStore(file, new AppData()), file);
    }

    static TimerDef Countdown() => new()
    {
        Name = "Farm",
        Kind = TimerKind.Countdown,
        Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(30) },
    };

    [Fact]
    public void Upsert_adds_then_replaces_and_persists()
    {
        using var dir = new TempDir();
        var (store, file) = NewStore(dir);
        var timer = Countdown();

        store.Upsert(timer);
        store.Upsert(timer with { Name = "Grind" });

        Assert.Equal("Grind", store.Current.Timers.Single().Name);
        Assert.Equal("Grind", file.Load().Value.Timers.Single().Name);
    }

    [Fact]
    public void Delete_removes_timer_and_its_mutes()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var timer = Countdown();
        store.Upsert(timer);
        store.ToggleMute(timer.Id, T0);

        store.Delete(timer.Id);

        Assert.Empty(store.Current.Timers);
        Assert.Empty(store.Current.Muted);
    }

    [Fact]
    public void ToggleMute_toggles()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var id = Guid.NewGuid();

        store.ToggleMute(id, T0);
        Assert.Single(store.Current.Muted);
        store.ToggleMute(id, T0);
        Assert.Empty(store.Current.Muted);
    }

    [Fact]
    public void Countdown_controls_update_state()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var timer = Countdown();
        store.Upsert(timer);

        store.StartCountdown(timer.Id, T0);
        Assert.Equal(T0.AddMinutes(30), store.Current.Timers.Single().Countdown!.EndsAtUtc);
        store.PauseCountdown(timer.Id, T0.AddMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(20), store.Current.Timers.Single().Countdown!.Remaining);
        store.ResumeCountdown(timer.Id, T0.AddMinutes(15));
        Assert.Equal(T0.AddMinutes(35), store.Current.Timers.Single().Countdown!.EndsAtUtc);
        store.ResetCountdown(timer.Id);
        Assert.Equal(CountdownStatus.Idle, store.Current.Timers.Single().Countdown!.Status);
    }

    [Fact]
    public void CompleteCountdowns_returns_finished_ones_and_sets_them_ready()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var done = Countdown();
        var running = Countdown() with { Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(90) } };
        store.Upsert(done);
        store.Upsert(running);
        store.StartCountdown(done.Id, T0);
        store.StartCountdown(running.Id, T0);

        var completed = store.CompleteCountdowns(T0.AddMinutes(30), T0.AddMinutes(30));

        Assert.Equal(done.Id, completed.Single().Id);
        Assert.Equal(CountdownStatus.Idle, store.Current.Timers.Single(t => t.Id == done.Id).Countdown!.Status);
        Assert.Equal(CountdownStatus.Running, store.Current.Timers.Single(t => t.Id == running.Id).Countdown!.Status);
    }

    [Fact]
    public void No_op_updates_do_not_raise_changed()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var raised = 0;
        store.Changed += () => raised++;

        store.PruneMuted(T0);
        store.CompleteCountdowns(T0, T0);

        Assert.Equal(0, raised);
        Assert.False(File.Exists(dir.File("timers.json")));
    }

    [Fact]
    public void Failed_save_leaves_state_unchanged_and_throws()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var raised = 0;
        store.Changed += () => raised++;
        Directory.CreateDirectory(dir.File("timers.json"));

        Assert.Throws<StateSaveException>(() => store.Upsert(Countdown()));

        Assert.Empty(store.Current.Timers);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ResetBossAlerts_returns_bosses_to_the_defaults_and_leaves_the_rest()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var tuned = new AlertConfig
        {
            LeadTimesMinutes = [30],
            Sound = new SoundAlert { Key = "harp" },
            Tts = new TtsAlert { Template = "{name} soon" },
            Overlay = new OverlayAlert { Enabled = true },
        };
        var slots = new ScheduledSpec { Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(22, 15))] };
        store.Upsert(new TimerDef { Name = "Kzarka", IsBuiltIn = true, Enabled = false, Alerts = tuned, Scheduled = slots });
        store.Upsert(Countdown() with { Alerts = tuned });

        store.ResetBossAlerts();

        var boss = store.Current.Timers.Single(t => t.IsBuiltIn);
        Assert.True(boss.Enabled);
        Assert.Equal(new AlertConfig(), boss.Alerts);
        Assert.Same(slots, boss.Scheduled);
        Assert.Equal(tuned, store.Current.Timers.Single(t => !t.IsBuiltIn).Alerts);
    }

    [Fact]
    public void PruneMuted_drops_past_entries()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        store.ToggleMute(Guid.NewGuid(), T0);
        store.ToggleMute(Guid.NewGuid(), T0.AddHours(2));

        store.PruneMuted(T0.AddHours(1));

        Assert.Equal(T0.AddHours(2), store.Current.Muted.Single().OccurrenceUtc);
    }

    [Fact]
    public void Horse_registrations_run_independently_up_to_ten_and_free_a_slot_on_completion()
    {
        using var dir = new TempDir();
        var (store, file) = NewStore(dir);
        var template = Presets.CreateHorseRegistration();
        store.Upsert(template);

        for (var i = 0; i < TimerStore.MaxHorseRegistrations; i++)
            Assert.Equal(HorseStartResult.Started, store.StartHorseRegistration(T0.AddSeconds(i)));
        Assert.Equal(HorseStartResult.LimitReached, store.StartHorseRegistration(T0.AddMinutes(1)));
        var runs = store.Current.Timers.Where(t => t.Preset == Presets.HorseRegistrationRun).ToList();
        Assert.Equal(10, runs.Count);
        Assert.Equal(10, runs.Select(t => t.Id).Distinct().Count());
        Assert.Equal(10, runs.Select(t => t.Countdown!.EndsAtUtc).Distinct().Count());
        Assert.Equal(10, file.Load().Value.Timers.Count(t => t.Preset == Presets.HorseRegistrationRun));
        store.PauseCountdown(runs[1].Id, T0.AddMinutes(1));
        Assert.Equal(HorseStartResult.LimitReached, store.StartHorseRegistration(T0.AddMinutes(1)));

        var completed = store.CompleteCountdowns(T0.AddMinutes(10), T0.AddMinutes(10));
        Assert.Single(completed);
        Assert.Equal(1, completed[0].HorseRunNumber);
        Assert.Equal(HorseStartResult.Started, store.StartHorseRegistration(T0.AddMinutes(10)));
        Assert.Equal(10, store.Current.Timers.Count(t => t.Preset == Presets.HorseRegistrationRun));
        Assert.Equal(1, store.Current.Timers.Last().HorseRunNumber);
        Assert.Equal(CountdownStatus.Idle, store.Current.Timers.Single(t => t.Id == template.Id).Countdown!.Status);
    }

    [Fact]
    public void Horse_hotkey_cannot_start_after_the_preset_is_deleted()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);

        Assert.Equal(HorseStartResult.Unavailable, store.StartHorseRegistration(T0));
        Assert.Empty(store.Current.Timers);
    }

    [Fact]
    public void Farm_remains_running_after_harvest_time_and_keeps_negative_time_when_paused()
    {
        using var dir = new TempDir();
        var (store, _) = NewStore(dir);
        var farm = Presets.Create().Single(t => t.Preset == Presets.Farm) with
        {
            Countdown = new CountdownSpec { Duration = TimeSpan.FromHours(1) },
        };
        store.Upsert(farm);
        store.StartCountdown(farm.Id, T0);

        Assert.Empty(store.CompleteCountdowns(T0.AddMinutes(90), T0.AddMinutes(90)));
        store.PauseCountdown(farm.Id, T0.AddMinutes(90));

        Assert.Equal(CountdownStatus.Paused, store.Current.Timers.Single().Countdown!.Status);
        Assert.Equal(TimeSpan.FromMinutes(-30), store.Current.Timers.Single().Countdown!.Remaining);
    }
}
