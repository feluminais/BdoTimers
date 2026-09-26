using BdoTimers.Core.Model;
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
}
