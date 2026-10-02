using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class CustomCountdownControlsTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Control_starts_pauses_and_resumes_with_alerts_off_and_persists_each_transition()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var timer = new TimerDef
        {
            Name = "Grind", Kind = TimerKind.Countdown, Enabled = false,
            Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(30) },
            ControlHotkey = new Hotkey(HotkeyModifiers.Ctrl, 0x47),
        };
        var store = new TimerStore(file, new AppData { Timers = [timer] });
        var clock = new FakeClock(Now);

        store.ControlCustomCountdown(timer.Id, clock);
        Assert.Equal(Now.AddMinutes(30), Saved().Countdown!.EndsAtUtc);
        clock.UtcNow = Now.AddMinutes(10);
        store.ControlCustomCountdown(timer.Id, clock);
        Assert.Equal(CountdownStatus.Paused, Saved().Countdown!.Status);
        Assert.Equal(TimeSpan.FromMinutes(20), Saved().Countdown!.Remaining);
        Assert.Null(Saved().Countdown!.EndsAtUtc);

        // Reload while paused: time spent closed must not count against the run.
        store = new TimerStore(file, file.Load().Value);
        clock.UtcNow = Now.AddHours(2);
        store.ControlCustomCountdown(timer.Id, clock);
        Assert.Equal(CountdownStatus.Running, Saved().Countdown!.Status);
        Assert.Equal(Now.AddHours(2).AddMinutes(20), Saved().Countdown!.EndsAtUtc);
        Assert.Equal(Now, Saved().Countdown!.StartedAtUtc);
        Assert.Equal(timer.ControlHotkey, Saved().ControlHotkey);
        Assert.False(Saved().Enabled);

        store.ResetCountdown(timer.Id);
        Assert.Equal(CountdownStatus.Idle, Saved().Countdown!.Status);
        store.ControlCustomCountdown(timer.Id, clock);
        Assert.Equal(clock.UtcNow.AddMinutes(30), Saved().Countdown!.EndsAtUtc);

        TimerDef Saved() => file.Load().Value.Timers.Single();
    }

    [Fact]
    public void Control_targets_only_custom_countdowns_and_ignores_deleted_timers()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var other = new TimerDef { Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };
        var horse = Presets.CreateHorseRegistration();
        var run = horse with
        {
            Id = Guid.NewGuid(), Preset = Presets.HorseRegistrationRun, StartHotkey = null,
            HorseRunNumber = 1, Countdown = CountdownOps.Start(horse.Countdown!, Now),
        };
        var data = new AppData { Timers = [.. Presets.Create(), horse, run, other, new TimerDef { Kind = TimerKind.Scheduled }] };
        var store = new TimerStore(file, data);
        foreach (var timer in data.Timers.Where(t => t.Id != other.Id))
            store.ControlCustomCountdown(timer.Id, new FakeClock(Now));
        Assert.Equal(data, store.Current);
        store.Delete(other.Id);
        var deleted = store.Current;
        store.ControlCustomCountdown(other.Id, new FakeClock(Now));
        Assert.Same(deleted, store.Current);
    }

    [Fact]
    public void Editing_and_clearing_a_hotkey_survives_reload_and_preserves_a_running_timer()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var timer = new TimerDef
        {
            Kind = TimerKind.Countdown, Countdown = CountdownOps.Start(new CountdownSpec(), Now),
        };
        var store = new TimerStore(file, new AppData { Timers = [timer] });
        var key = new Hotkey(HotkeyModifiers.Alt, 0x47);
        store.Modify(timer.Id, t => t with { ControlHotkey = key });
        Assert.Equal(key, file.Load().Value.Timers.Single().ControlHotkey);
        store.Modify(timer.Id, t => t with { ControlHotkey = null });
        Assert.Null(file.Load().Value.Timers.Single().ControlHotkey);
        Assert.Equal(timer.Countdown, file.Load().Value.Timers.Single().Countdown);
    }

    [Fact]
    public void Completed_countdown_keeps_its_binding_and_the_next_control_starts_a_full_run()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var timer = new TimerDef
        {
            Kind = TimerKind.Countdown, Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(2) },
            ControlHotkey = new Hotkey(HotkeyModifiers.Ctrl, 0x47),
        };
        var store = new TimerStore(file, new AppData { Timers = [timer] });
        var clock = new FakeClock(Now);
        store.ControlCustomCountdown(timer.Id, clock);
        clock.UtcNow = Now.AddMinutes(2);
        Assert.Single(store.CompleteCountdowns(clock.UtcNow, clock.UtcNow));
        Assert.Equal(CountdownStatus.Idle, store.Current.Timers.Single().Countdown!.Status);
        var target = new HotkeyTarget(HotkeyAction.ControlCountdown, timer.Id);
        Assert.Equal(timer.ControlHotkey, HotkeyCatalog.Active(store.Current, new OverlaySettings())[target]);
        store.ControlCustomCountdown(timer.Id, clock);
        Assert.Equal(Now.AddMinutes(4), file.Load().Value.Timers.Single().Countdown!.EndsAtUtc);
    }

    [Fact]
    public void Failed_control_save_preserves_the_current_run_and_publishes_no_change()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var timer = new TimerDef { Kind = TimerKind.Countdown, Countdown = CountdownOps.Start(new CountdownSpec(), Now) };
        var initial = new AppData { Timers = [timer] };
        var store = new TimerStore(file, initial);
        var raised = 0;
        store.Changed += () => raised++;
        Directory.CreateDirectory(file.FilePath);
        Assert.Throws<StateSaveException>(() => store.ControlCustomCountdown(timer.Id, new FakeClock(Now.AddMinutes(10))));
        Assert.Same(initial, store.Current);
        Assert.Equal(0, raised);
    }
}
