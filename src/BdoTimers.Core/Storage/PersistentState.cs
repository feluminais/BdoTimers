namespace BdoTimers.Core.Storage;

/// <summary>
/// Thread-safe holder of an immutable value. <see cref="Update"/> saves and raises
/// <see cref="Changed"/> only when the change function returns a different instance.
/// <see cref="Changed"/> is raised on the calling thread.
/// </summary>
public class PersistentState<T>(JsonFileStore<T> file, T initial) where T : class
{
    readonly object _gate = new();
    T _value = initial;

    public event Action? Changed;

    public T Current
    {
        get { lock (_gate) return _value; }
    }

    public void Update(Func<T, T> change)
    {
        bool changed;
        lock (_gate)
        {
            var next = change(_value);
            changed = !ReferenceEquals(next, _value);
            if (changed)
            {
                _value = next;
                file.Save(next);
            }
        }
        if (changed) Changed?.Invoke();
    }
}
