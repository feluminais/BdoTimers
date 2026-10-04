using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

/// <summary>Retains successful destructive changes briefly; undo uses the saved records and absolute timestamps.</summary>
public sealed class UndoHistory : IDisposable
{
    readonly TimerStore _timers;
    readonly TodoStore _todos;
    readonly IClock _clock;
    readonly Action<string>? _releaseImage;
    readonly int _capacity;
    readonly TimeSpan _lifetime;
    readonly List<Entry> _entries = [];

    public UndoHistory(TimerStore timers, TodoStore todos, IClock clock, Action<string>? releaseImage = null,
        int capacity = 10, TimeSpan? lifetime = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _lifetime = lifetime ?? TimeSpan.FromSeconds(15);
        if (_lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
        _timers = timers;
        _todos = todos;
        _clock = clock;
        _releaseImage = releaseImage;
        _capacity = capacity;
    }

    public event Action? Changed;
    public bool CanUndo => _entries.Any(e => e.ExpiresAtUtc > _clock.UtcNow);
    public string Message => _entries.LastOrDefault(e => e.ExpiresAtUtc > _clock.UtcNow)?.Message ?? "";

    public bool DeleteTimer(Guid id) => DeleteTimer(id, "Timer deleted");

    /// <summary>Removes a boss, bundled or added; a reset to the region's timetable brings bundled ones back.</summary>
    public bool DeleteBoss(Guid id) => DeleteTimer(id, "Boss removed");

    bool DeleteTimer(Guid id, string message)
    {
        var deleted = _timers.DeleteForUndo(id);
        if (deleted is null) return false;
        Add(message, () => _timers.RestoreDeleted(deleted), deleted.Timer.ImageFile);
        return true;
    }

    public bool ResetTimer(Guid id)
    {
        if (_timers.Current.Timers.Any(t => t.Id == id && t.Preset == Presets.HorseRegistrationRun))
            return DeleteTimer(id, "Timer reset");
        var reset = _timers.ResetForUndo(id);
        if (reset is null) return false;
        Add("Timer reset", () => _timers.RestoreReset(reset));
        return true;
    }

    public bool DeleteTodoList(Guid id)
    {
        var deleted = _todos.DeleteForUndo(id);
        if (deleted is null) return false;
        Add("List deleted", () => _todos.RestoreDeleted(deleted));
        return true;
    }

    void Add(string message, Func<bool> restore, string? image = null)
    {
        Refresh();
        _entries.Add(new(message, _clock.UtcNow + _lifetime, restore, image));
        while (_entries.Count > _capacity) RemoveAt(0);
        Changed?.Invoke();
    }

    public bool TryUndo()
    {
        Refresh();
        if (_entries.Count == 0) return false;
        // Keep the entry when persistence fails, so the user can retry.
        var restored = _entries[^1].Restore();
        RemoveAt(_entries.Count - 1);
        Changed?.Invoke();
        return restored;
    }

    public void Refresh()
    {
        var changed = false;
        var now = _clock.UtcNow;
        for (var i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].ExpiresAtUtc <= now)
            {
                RemoveAt(i);
                changed = true;
            }
        if (changed) Changed?.Invoke();
    }

    public void Dismiss()
    {
        Refresh();
        if (_entries.Count == 0) return;
        RemoveAt(_entries.Count - 1);
        Changed?.Invoke();
    }

    public void ReleasePicture(string? image)
    {
        if (image is not null && _entries.All(e => !string.Equals(e.Image, image, StringComparison.OrdinalIgnoreCase))
            && _timers.Current.Timers.All(t => !string.Equals(t.ImageFile, image, StringComparison.OrdinalIgnoreCase)))
            _releaseImage?.Invoke(image);
    }

    void RemoveAt(int index)
    {
        var image = _entries[index].Image;
        _entries.RemoveAt(index);
        ReleasePicture(image);
    }

    public void Dispose()
    {
        while (_entries.Count > 0) RemoveAt(_entries.Count - 1);
        Changed?.Invoke();
    }

    sealed record Entry(string Message, DateTimeOffset ExpiresAtUtc, Func<bool> Restore, string? Image);
}

internal sealed record DeletedTimer(TimerDef Timer, int Index, IReadOnlyList<MutedOccurrence> Muted, IReadOnlyList<TimerDef> Completed);
internal sealed record ResetTimer(TimerDef Before, TimerDef After, IReadOnlyList<TimerDef> Completed);
internal sealed record DeletedTodoList(TodoList List, int Index);
