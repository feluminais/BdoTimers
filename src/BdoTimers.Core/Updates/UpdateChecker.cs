using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Updates;

public sealed class UpdateChecker(IReleaseSource source, string installed, IClock clock,
    PersistentState<UpdateCheckState> state, bool automaticEnabled = true, TimeSpan? timeout = null,
    CancellationToken shutdown = default)
{
    readonly object _gate = new();
    Task<UpdateResult>? _running;
    UpdateResult _current = new(UpdateStatus.Idle);

    /// <summary>Raised on the checking thread; UI listeners must dispatch to their own thread.</summary>
    public event Action? Changed;
    public UpdateResult Current => Volatile.Read(ref _current);

    public Task<UpdateResult> CheckAsync()
    {
        lock (_gate) return _running ?? StartCheck();
    }

    /// <summary>A rolling 24-hour limit includes unsuccessful and manual attempts, and survives restarts.</summary>
    public async Task<UpdateResult?> CheckAutomaticallyAsync()
    {
        Task<UpdateResult> check;
        lock (_gate)
        {
            if (!automaticEnabled || _running is not null
                || state.Current.LastAttemptUtc is { } last && clock.UtcNow - last < TimeSpan.FromDays(1)) return null;
            check = StartCheck();
        }
        return await check.ConfigureAwait(false);
    }

    Task<UpdateResult> StartCheck()
    {
        // Assign before starting: synchronous failures and very fast responses must also share one request.
        var completion = new TaskCompletionSource<UpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _running = completion.Task;
        _ = RunCheckAsync(completion);
        return task;
    }

    async Task RunCheckAsync(TaskCompletionSource<UpdateResult> completion)
    {
        UpdateResult result;
        try
        {
            // Save the attempt before networking so offline starts and crashes do not repeatedly contact GitHub.
            state.Update(s => s with { LastAttemptUtc = clock.UtcNow });
            Publish(new(UpdateStatus.Checking));
            if (!ReleaseVersion.TryParse(installed, out var current)) throw new InvalidOperationException("Invalid installed product version.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));
            var release = await source.LatestStableAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            result = release is not null && release.Version > current
                ? new(UpdateStatus.UpdateAvailable, release) : new(UpdateStatus.UpToDate);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't check for updates", ex);
            result = new(UpdateStatus.CouldNotCheck);
        }
        Publish(result);
        lock (_gate) _running = null;
        completion.SetResult(result);
    }

    void Publish(UpdateResult result)
    {
        Volatile.Write(ref _current, result);
        if (Changed is not { } changed) return;
        foreach (Action listener in changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception ex) { Log.Error("Update status listener failed", ex); }
        }
    }
}
