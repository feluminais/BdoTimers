using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>Platform registration; a refused combo returns false instead of throwing.</summary>
public interface IHotkeyRegistration
{
    bool Register(int id, Hotkey key);
    void Unregister(int id);
}

/// <summary>Owns global bindings and releases them during input capture. Used on the UI thread.</summary>
public sealed class HotkeyRegistry(IHotkeyRegistration registration) : IDisposable
{
    Dictionary<HotkeyTarget, Hotkey> _wanted = [];
    readonly Dictionary<int, HotkeyTarget> _held = [];
    HashSet<HotkeyTarget> _refused = [];
    int _suspended;
    int _nextId;
    bool _disposed;

    public event Action? RefusedChanged;
    public IReadOnlySet<HotkeyTarget> Refused => _refused;

    public void Set(IReadOnlyDictionary<HotkeyTarget, Hotkey> wanted)
    {
        if (_disposed || (wanted.Count == _wanted.Count
            && wanted.All(pair => _wanted.TryGetValue(pair.Key, out var key) && key == pair.Value))) return;
        _wanted = wanted.ToDictionary();
        Apply();
    }

    public HotkeyTarget? TargetFor(int id) => _held.TryGetValue(id, out var target) ? target : null;

    public void Suspend()
    {
        _suspended++;
        Apply();
    }

    public void Resume()
    {
        _suspended = Math.Max(0, _suspended - 1);
        Apply();
    }

    void Release()
    {
        foreach (var id in _held.Keys) registration.Unregister(id);
        _held.Clear();
    }

    void Apply()
    {
        Release();
        if (_disposed || _suspended > 0) return;
        var refused = new HashSet<HotkeyTarget>();
        foreach (var (target, key) in _wanted)
        {
            // Fresh IDs ignore queued messages for released bindings; application IDs stop before 0xC000.
            _nextId = _nextId % 0xBFFF + 1;
            if (registration.Register(_nextId, key)) _held[_nextId] = target;
            else refused.Add(target);
        }
        if (refused.SetEquals(_refused)) return;
        _refused = refused;
        RefusedChanged?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        Release();
    }
}
