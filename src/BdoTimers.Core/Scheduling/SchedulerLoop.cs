using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Scheduling;

/// <summary>Drives <see cref="SchedulerEngine.Tick"/> once per second on a background thread.</summary>
public sealed class SchedulerLoop(SchedulerEngine engine, IClock clock,
    Action<Exception>? onFailure = null, Action<Exception>? onRecovered = null) : IDisposable
{
    readonly CancellationTokenSource _cts = new();
    readonly RepeatingErrorLog _errors = new("Scheduler tick", clock);
    Task? _run;
    Exception? _failure;

    public void Start() => _run = Task.Run(RunAsync);

    async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                engine.Tick();
                _errors.Succeeded();
                if (_failure is { } recovered)
                {
                    _failure = null;
                    // Storage recovery belongs to a successful save of the affected file.
                    if (recovered is not BdoTimers.Core.Storage.StateSaveException) Notify(onRecovered, recovered);
                }
            }
            catch (Exception ex)
            {
                _errors.Failed(ex);
                _failure = ex;
                Notify(onFailure, ex);
            }
        }
        while (await WaitAsync(timer));
    }

    static void Notify(Action<Exception>? callback, Exception error)
    {
        try { callback?.Invoke(error); }
        catch (Exception ex) { Log.Error("Scheduler status reporting failed", ex); }
    }

    async Task<bool> WaitAsync(PeriodicTimer timer)
    {
        try { return await timer.WaitForNextTickAsync(_cts.Token); }
        catch (OperationCanceledException) { return false; }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _run?.Wait(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
