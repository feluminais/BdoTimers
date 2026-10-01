using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

// A timer that can't be scheduled is logged.
[Collection(nameof(Log))]
public class SchedulerEngineTests : IDisposable
{
    static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    sealed class RecordingSink : IAlertSink
    {
        public List<AlertEvent> Alerts { get; } = [];
        public List<TimerDef> EndedWhileAway { get; } = [];
        public void Dispatch(AlertEvent alert) => Alerts.Add(alert);
        public void NotifyEndedWhileAway(TimerDef timer) => EndedWhileAway.Add(timer);
        public List<IReadOnlyCollection<string>> SpeechPrepared { get; } = [];
        public void PrepareSpeech(IReadOnlyCollection<string> texts) => SpeechPrepared.Add(texts);
    }

    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(T0);
    readonly RecordingSink _sink = new();
    readonly TimerStore _timers;
    readonly PersistentState<AppSettings> _settings;
    readonly SchedulerEngine _engine;

    public SchedulerEngineTests()
    {
        _timers = new TimerStore(new JsonFileStore<AppData>(_dir.File("t.json"), () => new AppData()), new AppData());
        _settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(_dir.File("s.json"), () => new AppSettings()), new AppSettings());
        _engine = new SchedulerEngine(_timers, _settings, _sink, _clock);
    }

    public void Dispose() => _dir.Dispose();

    TimerDef AddCountdown()
    {
        var timer = new TimerDef
        {
            Name = "Farm",
            Kind = TimerKind.Countdown,
            Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(10) },
            Alerts = new AlertConfig { LeadTimesMinutes = [5, 0] },
        };
        _timers.Upsert(timer);
        _timers.StartCountdown(timer.Id, T0);
        return timer;
    }

    void TickAt(DateTimeOffset at)
    {
        _clock.UtcNow = at;
        _engine.Tick();
    }

    [Fact]
    public void Countdown_alerts_then_goes_idle()
    {
        var timer = AddCountdown();

        TickAt(T0.AddMinutes(5));
        TickAt(T0.AddMinutes(10));

        Assert.Equal(new[] { 5, 0 }, _sink.Alerts.Select(a => a.LeadMinutes));
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single(t => t.Id == timer.Id).Countdown!.Status);
    }

    [Fact]
    public void Timers_without_their_own_times_follow_the_default_in_settings()
    {
        _settings.Update(s => s with { DefaultLeadTimesMinutes = [2] });
        var timer = new TimerDef
        {
            Name = "Farm",
            Kind = TimerKind.Countdown,
            Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(10) },
        };
        _timers.Upsert(timer);
        _timers.StartCountdown(timer.Id, T0);

        TickAt(T0.AddMinutes(5));
        TickAt(T0.AddMinutes(8));

        Assert.Equal(new[] { 2 }, _sink.Alerts.Select(a => a.LeadMinutes));
    }

    [Fact]
    public void Speech_is_prepared_in_the_minute_before_a_spoken_alert()
    {
        AddCountdown(); // alerts at 5 and 0 minutes left, so at T0 + 5 and T0 + 10 minutes

        TickAt(T0.AddMinutes(3));
        Assert.Empty(_sink.SpeechPrepared);

        TickAt(T0.AddMinutes(4).AddSeconds(30));
        Assert.Equal(["Farm in 5 minutes", "Farm now"], _sink.SpeechPrepared.Single());
    }

    [Fact]
    public void Silent_timers_prepare_no_speech()
    {
        var timer = AddCountdown();
        _timers.Modify(timer.Id, t => t with { Alerts = t.Alerts with { Tts = new TtsAlert { Enabled = false } } });

        TickAt(T0.AddMinutes(4).AddSeconds(30));

        Assert.Empty(_sink.SpeechPrepared);
    }

    [Fact]
    public void Paused_alerts_prepare_no_speech()
    {
        AddCountdown();
        _settings.Update(s => s with { AlertsPausedUntilUtc = DateTimeOffset.MaxValue });

        TickAt(T0.AddMinutes(4).AddSeconds(30));

        Assert.Empty(_sink.SpeechPrepared);
    }

    [Fact]
    public void Paused_alerts_are_dropped_not_replayed()
    {
        AddCountdown();
        _settings.Update(s => s with { AlertsPausedUntilUtc = DateTimeOffset.MaxValue });

        TickAt(T0.AddMinutes(5));
        _settings.Update(s => s with { AlertsPausedUntilUtc = null });
        TickAt(T0.AddMinutes(5).AddSeconds(1));

        Assert.Empty(_sink.Alerts);
    }

    [Fact]
    public void Setting_the_clock_back_keeps_a_skipped_spawn_skipped()
    {
        var kzarka = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 5, 0); // spawns at T0
        _timers.Upsert(kzarka);
        _timers.ToggleMute(kzarka.Id, T0);

        TickAt(T0.AddMinutes(50));
        TickAt(T0.AddMinutes(-5));
        TickAt(T0);

        Assert.Empty(_sink.Alerts);
    }

    [Fact]
    public void Bosses_spawning_together_raise_one_alert()
    {
        var kzarka = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 0);
        var uturi = TestTimers.Scheduled("Uturi", DayOfWeek.Tuesday, 14, 0, 0);
        _timers.Upsert(kzarka);
        _timers.Upsert(uturi);

        TickAt(T0);

        Assert.Equal(new[] { "Kzarka", "Uturi" }, _sink.Alerts.Single().Timers.Select(t => t.Name));
    }

    [Fact]
    public void A_timer_that_cant_be_scheduled_doesnt_stop_other_alerts()
    {
        var broken = TestTimers.Scheduled("Broken", DayOfWeek.Tuesday, 12, 10, 5, 0);
        _timers.Upsert(broken with { Scheduled = broken.Scheduled! with { TimeZoneId = "Nowhere/Nothing" } });
        var timer = AddCountdown();

        TickAt(T0.AddMinutes(5));
        TickAt(T0.AddMinutes(10));

        Assert.Equal(new[] { 5, 0 }, _sink.Alerts.Select(a => a.LeadMinutes));
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single(t => t.Id == timer.Id).Countdown!.Status);
    }

    [Fact]
    public void Startup_reports_countdowns_that_ended_while_closed()
    {
        var timer = AddCountdown();
        _clock.UtcNow = T0.AddMinutes(30);

        _engine.ReconcileStartup();
        _engine.Tick();

        Assert.Equal(timer.Id, _sink.EndedWhileAway.Single().Id);
        Assert.Empty(_sink.Alerts);
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single().Countdown!.Status);
    }

    [Fact]
    public void A_countdown_that_ended_while_asleep_is_reported()
    {
        var timer = AddCountdown(); // ends at T0 + 10 minutes

        TickAt(T0.AddMinutes(5));
        TickAt(T0.AddMinutes(40));

        Assert.Equal(new[] { 5 }, _sink.Alerts.Select(a => a.LeadMinutes));
        Assert.Equal(timer.Id, _sink.EndedWhileAway.Single().Id);
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single().Countdown!.Status);
    }

    [Fact]
    public void A_countdown_that_ended_during_a_stall_longer_than_the_grace_is_reported()
    {
        AddCountdown();

        TickAt(T0.AddMinutes(10).AddSeconds(-1));
        TickAt(T0.AddMinutes(10) + AlertPlanner.Grace + TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(_sink.Alerts, a => a.LeadMinutes == 0);
        Assert.Single(_sink.EndedWhileAway);
    }

    [Fact]
    public void A_countdown_alerted_at_its_end_is_not_reported_again()
    {
        AddCountdown();

        TickAt(T0.AddMinutes(10) + AlertPlanner.Grace);
        TickAt(T0.AddMinutes(40));

        Assert.Equal(0, _sink.Alerts.Single().LeadMinutes);
        Assert.Empty(_sink.EndedWhileAway);
    }

    [Fact]
    public void Paused_alerts_dont_report_countdowns_that_ended_while_asleep()
    {
        AddCountdown();
        _settings.Update(s => s with { AlertsPausedUntilUtc = DateTimeOffset.MaxValue });

        TickAt(T0.AddMinutes(40));

        Assert.Empty(_sink.EndedWhileAway);
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single().Countdown!.Status);
    }

    [Fact]
    public void Farm_alerts_at_harvest_then_keeps_growing_without_replaying_on_startup()
    {
        var farm = Presets.Create().Single(t => t.Preset == Presets.Farm) with
        {
            Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(10) },
            Alerts = new AlertConfig { LeadTimesMinutes = [0] },
        };
        _timers.Upsert(farm);
        _timers.StartCountdown(farm.Id, T0);

        TickAt(T0.AddMinutes(10));
        TickAt(T0.AddMinutes(12));
        _clock.UtcNow = T0.AddMinutes(30);
        _engine.ReconcileStartup();

        Assert.Single(_sink.Alerts);
        Assert.Empty(_sink.EndedWhileAway);
        Assert.Equal(CountdownStatus.Running, _timers.Current.Timers.Single().Countdown!.Status);
        Assert.Equal(T0.AddMinutes(10), _timers.Current.Timers.Single().Countdown!.EndsAtUtc);
    }
}
