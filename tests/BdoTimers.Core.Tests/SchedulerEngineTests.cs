using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class SchedulerEngineTests : IDisposable
{
    static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    sealed class RecordingSink : IAlertSink
    {
        public List<AlertEvent> Alerts { get; } = [];
        public List<TimerDef> EndedWhileAway { get; } = [];
        public void Dispatch(AlertEvent alert) => Alerts.Add(alert);
        public void NotifyEndedWhileAway(TimerDef timer) => EndedWhileAway.Add(timer);
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

    TimerDef AddCountdown(bool repeat = false)
    {
        var timer = new TimerDef
        {
            Name = "Farm",
            Kind = TimerKind.Countdown,
            Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(10), AutoRepeat = repeat },
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
    public void Repeating_countdown_restarts()
    {
        var timer = AddCountdown(repeat: true);

        TickAt(T0.AddMinutes(10));

        Assert.Equal(T0.AddMinutes(20), _timers.Current.Timers.Single(t => t.Id == timer.Id).Countdown!.EndsAtUtc);
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
}
