using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

[Collection(nameof(Log))]
public sealed class SchedulerLoopTests : IDisposable
{
    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(T0);
    readonly TimerStore _timers;
    readonly SchedulerEngine _engine;
    readonly string _timerPath;

    public SchedulerLoopTests()
    {
        _timerPath = _dir.File("timers.json");
        _timers = new(new(_timerPath, () => new()), new());
        var settings = new PersistentState<AppSettings>(new(_dir.File("settings.json"), () => new()), new());
        _engine = new(_timers, settings, new SilentSink(), _clock);
    }

    public void Dispose() => _dir.Dispose();

    static TimerDef DueCountdown() => new()
    {
        Name = "Due", Kind = TimerKind.Countdown,
        Countdown = new() { Status = CountdownStatus.Running, EndsAtUtc = T0, Duration = TimeSpan.FromMinutes(30) },
    };

    TimerDef InvalidEvent()
    {
        var timer = new TimerDef
        {
            Name = "Event", Kind = TimerKind.OneTime,
            OneTime = new() { Date = DateOnly.FromDateTime(T0.UtcDateTime), Time = TimeOnly.FromDateTime(T0.UtcDateTime).AddMinutes(10),
                TimeZoneId = "Invalid/TimeZone/ForTest" },
        };
        _timers.Upsert(timer);
        return timer;
    }

    void RepairEvent(TimerDef timer) => _timers.Modify(timer.Id,
        t => t with { OneTime = t.OneTime! with { TimeZoneId = "UTC" } });

    [Fact]
    public async Task Save_failure_reports_the_file_and_successful_retry_does_not_emit_generic_recovery()
    {
        var timer = DueCountdown();
        _timers.Upsert(timer);
        File.Delete(_timerPath);
        Directory.CreateDirectory(_timerPath);
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recoveries = 0;
        _timers.Changed += () => saved.TrySetResult();
        using (var loop = new SchedulerLoop(_engine, _clock, ex => failed.TrySetResult(ex), _ => Interlocked.Increment(ref recoveries)))
        {
            loop.Start();
            var error = Assert.IsType<StateSaveException>(await failed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(_timerPath, error.FilePath);
            Assert.Equal(CountdownStatus.Running, _timers.Current.Timers[0].Countdown!.Status);

            Directory.Delete(_timerPath);
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(0, recoveries);
        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers[0].Countdown!.Status);
        Assert.Equal(CountdownStatus.Idle, new JsonFileStore<AppData>(_timerPath, () => new()).Load().Value.Timers[0].Countdown!.Status);
    }

    [Fact]
    public async Task Scheduling_failure_reports_recovery_after_the_invalid_event_is_repaired()
    {
        var timer = InvalidEvent();
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var loop = new SchedulerLoop(_engine, _clock, ex => failed.TrySetResult(ex), ex => recovered.TrySetResult(ex));
        loop.Start();
        var error = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<TimeZoneNotFoundException>(error);

        RepairEvent(timer);
        var previous = await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(error, previous);
        Assert.Equal("UTC", _timers.Current.Timers[0].OneTime!.TimeZoneId);
    }

    [Fact]
    public async Task Throwing_failure_callback_does_not_stop_a_later_persistence_retry()
    {
        var timer = DueCountdown();
        _timers.Upsert(timer);
        File.Delete(_timerPath);
        Directory.CreateDirectory(_timerPath);
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _timers.Changed += () => saved.TrySetResult();
        using var loop = new SchedulerLoop(_engine, _clock, _ =>
        {
            failed.TrySetResult();
            throw new InvalidOperationException("Failure callback error");
        });
        loop.Start();
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Directory.Delete(_timerPath);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers[0].Countdown!.Status);
    }

    [Fact]
    public async Task Throwing_recovery_callback_does_not_stop_later_timer_completion()
    {
        var timer = InvalidEvent();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var loop = new SchedulerLoop(_engine, _clock, _ => failed.TrySetResult(), _ =>
        {
            recovered.TrySetResult();
            throw new InvalidOperationException("Recovery callback error");
        });
        loop.Start();
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        RepairEvent(timer);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var countdown = DueCountdown();
        _timers.Upsert(countdown);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _timers.Changed += () =>
        {
            if (_timers.Current.Timers.Any(t => t.Id == countdown.Id && t.Countdown?.Status == CountdownStatus.Idle))
                completed.TrySetResult();
        };

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CountdownStatus.Idle, _timers.Current.Timers.Single(t => t.Id == countdown.Id).Countdown!.Status);
    }

    sealed class SilentSink : IAlertSink
    {
        public void Dispatch(AlertEvent alert) { }
        public void NotifyEndedWhileAway(TimerDef timer) { }
        public void PrepareSpeech(IReadOnlyCollection<string> texts) { }
    }
}
