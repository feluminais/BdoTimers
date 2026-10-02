using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Diagnostics;

public sealed record FailureInfo(string Area, string Message, DateTimeOffset OccurredAt, int Count);

/// <summary>Active problems and their recovery, with repeated notices limited to once per five minutes.</summary>
public sealed class FailureTracker(IClock clock)
{
    readonly object _gate = new();
    readonly Dictionary<string, FailureInfo> _active = [];
    readonly Dictionary<string, DateTimeOffset> _noticed = [];
    FailureInfo? _latest;

    public IReadOnlyList<FailureInfo> Active
    {
        get { lock (_gate) return _active.Values.OrderBy(f => f.Area).ToArray(); }
    }

    public FailureInfo? Latest
    {
        get { lock (_gate) return _latest; }
    }

    public bool Report(string area, string message)
    {
        lock (_gate)
        {
            var now = clock.UtcNow;
            _active.TryGetValue(area, out var previous);
            var changed = previous is null || previous.Message != message;
            _latest = _active[area] = new(area, message, now, (previous?.Count ?? 0) + 1);
            if (!changed && _noticed.TryGetValue(area, out var last) && now - last < TimeSpan.FromMinutes(5)) return false;
            _noticed[area] = now;
            return true;
        }
    }

    public bool Clear(string area)
    {
        lock (_gate)
        {
            _noticed.Remove(area);
            return _active.Remove(area);
        }
    }
}
