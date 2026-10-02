using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class AlertEligibilityTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
    static AlertEvent Queued(TimerDef timer, int lead = 0) => new([timer], Now, lead, lead);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_or_deleted_custom_countdowns_cancel_queued_playback(bool delete)
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0);
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        var alert = Queued(timer);
        Assert.Same(alert, AlertEligibility.Filter(store.Current, alert));

        if (delete) store.Delete(timer.Id);
        else store.SetEnabled(timer.Id, false);

        Assert.Null(AlertEligibility.Filter(store.Current, alert));
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("pause")]
    [InlineData("restart")]
    [InlineData("duration")]
    public void Changing_a_countdown_run_cancels_its_queued_alert(string edit)
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0);
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        switch (edit)
        {
            case "reset": store.Reset(timer.Id); break;
            case "pause": store.Pause(timer.Id, Now.AddMinutes(-1)); break;
            case "restart": store.Start(timer.Id, Now); break;
            case "duration": store.Modify(timer.Id, t => t with
                { Countdown = CountdownOps.ChangeDuration(t.Countdown!, TimeSpan.FromHours(2)) }); break;
        }

        Assert.Null(AlertEligibility.Filter(store.Current, Queued(timer)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Natural_completion_preserves_only_the_end_alert_and_explicit_removal_cancels_it(bool horse)
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0) with { Preset = horse ? Presets.HorseRegistrationRun : null };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        var alert = Queued(timer);

        Assert.Equal(timer.Id, Assert.Single(store.CompleteCountdowns(Now, Now)).Id);

        Assert.Same(alert, AlertEligibility.Filter(store.Current, alert));
        Assert.Null(AlertEligibility.Filter(store.Current, Queued(timer, 5)));
        Assert.Equal(horse, store.Current.Timers.Count == 0);
        if (horse) store.Delete(timer.Id);
        else store.Reset(timer.Id);
        Assert.Null(AlertEligibility.Filter(store.Current, alert));
    }

    [Fact]
    public void Completed_run_snapshots_expire_and_are_not_persisted()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var timer = TestTimers.Countdown(Now, 0);
        var store = new TimerStore(file, new() { Timers = [timer] });
        store.CompleteCountdowns(Now, Now);

        Assert.Single(store.Current.CompletedCountdowns);
        Assert.Empty(file.Load().Value.CompletedCountdowns);
        store.PruneMuted(Now.AddTicks(1));
        Assert.Empty(store.Current.CompletedCountdowns);
        Assert.Null(AlertEligibility.Filter(store.Current, Queued(timer)));
    }

    [Fact]
    public void Disabling_a_horse_before_completion_does_not_revive_its_queued_end_alert()
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0) with { Preset = Presets.HorseRegistrationRun };
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        store.SetEnabled(timer.Id, false);

        store.CompleteCountdowns(Now, Now);

        Assert.Empty(store.Current.Timers);
        Assert.Null(AlertEligibility.Filter(store.Current, Queued(timer)));
    }

    [Fact]
    public void Failed_completion_save_does_not_authorize_removed_runs()
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0) with { Preset = Presets.HorseRegistrationRun };
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new());
        var store = new TimerStore(file, new() { Timers = [timer] });
        Directory.CreateDirectory(dir.File("timers.json.tmp"));

        Assert.Throws<StateSaveException>(() => store.CompleteCountdowns(Now, Now));

        Assert.Empty(store.Current.CompletedCountdowns);
        Assert.Same(timer, Assert.Single(store.Current.Timers));
    }

    [Fact]
    public void Custom_weekly_edits_mutes_and_disabling_are_rechecked()
    {
        using var dir = new TempDir();
        var timer = TestTimers.Scheduled("Weekly", DayOfWeek.Monday, 12, 0, 0);
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        var alert = Queued(timer);
        Assert.Same(alert, AlertEligibility.Filter(store.Current, alert));
        store.ToggleMute(timer.Id, Now);
        Assert.Null(AlertEligibility.Filter(store.Current, alert));
        store.ToggleMute(timer.Id, Now);
        store.SetWeeklyDateRange(timer.Id, new DateOnly(2026, 10, 6), null);
        Assert.Null(AlertEligibility.Filter(store.Current, alert));
        store.SetWeeklyDateRange(timer.Id, null, null);
        store.SetEnabled(timer.Id, false);
        Assert.Null(AlertEligibility.Filter(store.Current, alert));
        store.Delete(timer.Id);
        Assert.Null(AlertEligibility.Filter(store.Current, alert));
    }

    [Fact]
    public void Queued_playback_uses_current_names_and_channel_settings_without_repeated_refreshes()
    {
        using var dir = new TempDir();
        var timer = TestTimers.Countdown(Now, 0);
        var store = new TimerStore(new(dir.File("timers.json"), () => new()), new() { Timers = [timer] });
        store.Modify(timer.Id, t => t with { Name = "Renamed", Alerts = t.Alerts with { Sound = new() { Enabled = false } } });

        var filtered = AlertEligibility.Filter(store.Current, Queued(timer));

        var current = Assert.Single(filtered!.Timers);
        Assert.Equal("Renamed", current.Name);
        Assert.False(current.Alerts.Sound.Enabled);
        Assert.Same(filtered, AlertEligibility.Filter(store.Current, filtered));
    }
}
