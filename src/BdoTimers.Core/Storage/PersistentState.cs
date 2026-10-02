namespace BdoTimers.Core.Storage;

/// <summary>
/// Thread-safe holder of an immutable value. <see cref="Update"/> saves and raises
/// <see cref="Changed"/> only when the change function returns a different instance.
/// <see cref="Changed"/> is raised on the calling thread. If the save fails the value is left
/// unchanged and <see cref="StateSaveException"/> is thrown, so memory never runs ahead of disk.
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
                try { file.Save(next); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new StateSaveException(file.FilePath, ex);
                }
                _value = next;
            }
        }
        if (changed) Changed?.Invoke();
    }
}

public sealed class StateSaveException(string filePath, Exception inner)
    : IOException($"Couldn't save {Path.GetFileName(filePath)}: {inner.Message}", inner)
{
    public string FilePath { get; } = filePath;
}
