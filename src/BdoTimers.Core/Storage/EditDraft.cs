using System.Text.Json;

namespace BdoTimers.Core.Storage;

/// <summary>Edits a record privately, then applies only those edits to the latest live record.</summary>
public sealed class EditDraft<T>(T original) where T : class
{
    readonly string _original = JsonSerializer.Serialize(original);
    readonly T _source = original;
    readonly List<(string? Key, Func<T, T> Change)> _changes = [];
    public T Current { get; private set; } = original;
    public bool HasChanges => JsonSerializer.Serialize(Current) != _original;
    public event Action? Changed;

    public void Update(Func<T, T> change, string? key = null)
    {
        if (key is not null) _changes.RemoveAll(c => c.Key == key);
        _changes.Add((key, change));
        Current = _changes.Aggregate(_source, (value, edit) => edit.Change(value));
        Changed?.Invoke();
    }

    public T Apply(T latest) => HasChanges ? _changes.Aggregate(latest, (value, edit) => edit.Change(value)) : latest;
}
