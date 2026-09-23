using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Scheduling;

/// <summary>Drives <see cref="SchedulerEngine.Tick"/> once per second on a background thread.</summary>
public sealed class SchedulerLoop(SchedulerEngine engine) : IDisposable
{
    readonly CancellationTokenSource _cts = new();
    Task? _run;

    public void Start() => _run = Task.Run(RunAsync);

    async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try { engine.Tick(); }
            catch (Exception ex) { Log.Error("Scheduler tick failed", ex); }
        }
        while (await WaitAsync(timer));
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
