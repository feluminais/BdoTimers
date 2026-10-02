using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public sealed class TimerStoreTests : IDisposable
{
    readonly TempDir _dir = new();
    readonly JsonFileStore<AppData> _file;
    readonly TimerStore _store;

    public TimerStoreTests()
    {
        _file = new JsonFileStore<AppData>(_dir.File("timers.json"), () => new AppData());
        _store = new TimerStore(_file, new AppData());
    }

    public void Dispose() => _dir.Dispose();

    static TimerDef Countdown() => new()
    {
        Name = "Farm",
        Kind = TimerKind.Countdown,
        Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(30) },
    };

    [Fact]
    public void Upsert_adds_then_replaces_and_persists()
    {
        var timer = Countdown();

        _store.Upsert(timer);
        _store.Upsert(timer with { Name = "Grind" });

        Assert.Equal("Grind", _store.Current.Timers.Single().Name);
        Assert.Equal("Grind", _file.Load().Value.Timers.Single().Name);
    }

    [Fact]
    public void Delete_removes_timer_and_its_mutes()
    {
        var timer = Countdown();
        _store.Upsert(timer);
        _store.ToggleMute(timer.Id, T0);

        _store.Delete(timer.Id);

        Assert.Empty(_store.Current.Timers);
        Assert.Empty(_store.Current.Muted);
    }

    [Fact]
    public void ToggleMute_toggles()
    {
        var id = Guid.NewGuid();

        _store.ToggleMute(id, T0);
        Assert.Single(_store.Current.Muted);
        _store.ToggleMute(id, T0);
        Assert.Empty(_store.Current.Muted);
    }

    [Fact]
    public void Countdown_controls_update_state()
    {
        var timer = Countdown();
        _store.Upsert(timer);

        _store.Start(timer.Id, T0);
        Assert.Equal(T0.AddMinutes(30), _store.Current.Timers.Single().Countdown!.EndsAtUtc);
        _store.Pause(timer.Id, T0.AddMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(20), _store.Current.Timers.Single().Countdown!.Remaining);
        _store.Resume(timer.Id, T0.AddMinutes(15));
        Assert.Equal(T0.AddMinutes(35), _store.Current.Timers.Single().Countdown!.EndsAtUtc);
        _store.Reset(timer.Id);
        Assert.Equal(CountdownStatus.Idle, _store.Current.Timers.Single().Countdown!.Status);
    }

    [Fact]
    public void CompleteCountdowns_returns_finished_ones_and_sets_them_ready()
    {
        var done = Countdown();
        var running = Countdown() with { Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(90) } };
        _store.Upsert(done);
        _store.Upsert(running);
        _store.Start(done.Id, T0);
        _store.Start(running.Id, T0);

        var completed = _store.CompleteCountdowns(T0.AddMinutes(30), T0.AddMinutes(30));

        Assert.Equal(done.Id, completed.Single().Id);
        Assert.Equal(CountdownStatus.Idle, _store.Current.Timers.Single(t => t.Id == done.Id).Countdown!.Status);
        Assert.Equal(CountdownStatus.Running, _store.Current.Timers.Single(t => t.Id == running.Id).Countdown!.Status);
    }

    [Fact]
    public void No_op_updates_do_not_raise_changed()
    {
        var raised = 0;
        _store.Changed += () => raised++;

        _store.PruneMuted(T0);
        _store.CompleteCountdowns(T0, T0);

        Assert.Equal(0, raised);
        Assert.False(File.Exists(_dir.File("timers.json")));
    }

    [Fact]
    public void Failed_save_leaves_state_unchanged_and_throws()
    {
        var raised = 0;
        _store.Changed += () => raised++;
        Directory.CreateDirectory(_dir.File("timers.json"));

        Assert.Throws<StateSaveException>(() => _store.Upsert(Countdown()));

        Assert.Empty(_store.Current.Timers);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ResetBossAlerts_returns_bosses_to_the_defaults_and_leaves_the_rest()
    {
        var tuned = new AlertConfig
        {
            LeadTimesMinutes = [30],
            Sound = new SoundAlert { Key = "harp" },
            Tts = new TtsAlert { Template = "{name} soon" },
            Overlay = new OverlayAlert { Enabled = true },
        };
        var slots = new ScheduledSpec { Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(22, 15))] };
        _store.Upsert(new TimerDef { Name = "Kzarka", IsBuiltIn = true, Enabled = false, Alerts = tuned, Scheduled = slots });
        _store.Upsert(Countdown() with { Alerts = tuned });

        _store.ResetBossAlerts();

        var boss = _store.Current.Timers.Single(t => t.IsBuiltIn);
        Assert.True(boss.Enabled);
        Assert.Equal(new AlertConfig(), boss.Alerts);
        Assert.Same(slots, boss.Scheduled);
        Assert.Equal(tuned, _store.Current.Timers.Single(t => !t.IsBuiltIn).Alerts);
    }

    [Fact]
    public void PruneMuted_drops_past_entries()
    {
        _store.ToggleMute(Guid.NewGuid(), T0);
        _store.ToggleMute(Guid.NewGuid(), T0.AddHours(2));

        _store.PruneMuted(T0.AddHours(1));

        Assert.Equal(T0.AddHours(2), _store.Current.Muted.Single().OccurrenceUtc);
    }

    [Fact]
    public void Horse_registrations_run_independently_up_to_ten_and_free_a_slot_on_completion()
    {
        var template = Presets.CreateHorseRegistration();
        _store.Upsert(template);

        for (var i = 0; i < TimerStore.MaxHorseRegistrations; i++)
            Assert.Equal(HorseStartResult.Started, _store.StartHorseRegistration(T0.AddSeconds(i)));
        Assert.Equal(HorseStartResult.LimitReached, _store.StartHorseRegistration(T0.AddMinutes(1)));
        var runs = _store.Current.Timers.Where(t => t.Preset == Presets.HorseRegistrationRun).ToList();
        Assert.Equal(10, runs.Count);
        Assert.Equal(10, runs.Select(t => t.Id).Distinct().Count());
        Assert.Equal(10, runs.Select(t => t.Countdown!.EndsAtUtc).Distinct().Count());
        Assert.Equal(10, _file.Load().Value.Timers.Count(t => t.Preset == Presets.HorseRegistrationRun));
        _store.Pause(runs[1].Id, T0.AddMinutes(1));
        Assert.Equal(HorseStartResult.LimitReached, _store.StartHorseRegistration(T0.AddMinutes(1)));

        var completed = _store.CompleteCountdowns(T0.AddMinutes(10), T0.AddMinutes(10));
        Assert.Single(completed);
        Assert.Equal(1, completed[0].HorseRunNumber);
        Assert.Equal(HorseStartResult.Started, _store.StartHorseRegistration(T0.AddMinutes(10)));
        Assert.Equal(10, _store.Current.Timers.Count(t => t.Preset == Presets.HorseRegistrationRun));
        Assert.Equal(1, _store.Current.Timers.Last().HorseRunNumber);
        Assert.Equal(CountdownStatus.Idle, _store.Current.Timers.Single(t => t.Id == template.Id).Countdown!.Status);
    }

    [Fact]
    public void Horse_hotkey_cannot_start_after_the_preset_is_deleted()
    {
        Assert.Equal(HorseStartResult.Unavailable, _store.StartHorseRegistration(T0));
        Assert.Empty(_store.Current.Timers);
    }

    [Fact]
    public void Farm_remains_running_after_harvest_time_and_keeps_negative_time_when_paused()
    {
        var farm = Presets.Create().Single(t => t.Preset == Presets.Farm) with
        {
            Countdown = new CountdownSpec { Duration = TimeSpan.FromHours(1) },
        };
        _store.Upsert(farm);
        _store.Start(farm.Id, T0);

        Assert.Empty(_store.CompleteCountdowns(T0.AddMinutes(90), T0.AddMinutes(90)));
        _store.Pause(farm.Id, T0.AddMinutes(90));

        Assert.Equal(CountdownStatus.Paused, _store.Current.Timers.Single().Countdown!.Status);
        Assert.Equal(TimeSpan.FromMinutes(-30), _store.Current.Timers.Single().Countdown!.Remaining);
    }

    [Fact]
    public void ForgetSound_sets_timers_using_it_back_to_default()
    {
        TimerDef With(string name, SoundAlert sound) => new() { Name = name, Alerts = new AlertConfig { Sound = sound } };
        _store.Upsert(With("A", new SoundAlert { Key = "horn.wav" }));
        _store.Upsert(With("B", new SoundAlert { Key = "ping" }));
        _store.Upsert(With("C", new SoundAlert { Enabled = false }));

        _store.ForgetSound("horn.wav");

        var sounds = _store.Current.Timers.ToDictionary(t => t.Name, t => t.Alerts.Sound);
        Assert.Equal(new SoundAlert(), sounds["A"]);
        Assert.Equal(new SoundAlert { Key = "ping" }, sounds["B"]);
        Assert.Equal(new SoundAlert { Enabled = false }, sounds["C"]);
    }
}
