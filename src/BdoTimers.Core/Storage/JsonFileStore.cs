using System.Text.Json;
using BdoTimers.Core.Json;

namespace BdoTimers.Core.Storage;

public sealed record LoadResult<T>(T Value, string? RecoveredBackupPath);

public sealed class JsonFileStore<T>(string filePath, Func<T> createDefault) where T : class
{
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Invalid JSON or saved models are renamed to "*.bad-&lt;timestamp&gt;" and replaced with defaults.
    /// Filesystem failures propagate to the caller.
    /// </summary>
    public LoadResult<T> Load()
    {
        if (!File.Exists(FilePath)) return new(createDefault(), null);
        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath), JsonDefaults.Options);
            if (value is not null)
            {
                SavedDataValidation.Check(value);
                return new(value, null);
            }
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        catch (InvalidDataException) { }

        var backup = $"{FilePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(FilePath, backup, overwrite: true);
        return new(createDefault(), backup);
    }

    /// <summary>
    /// Writes a temp file, flushes it to disk, then renames it over the target, so a crash or power loss keeps either
    /// the old file or the complete new one. Without the flush the rename can reach the disk before the data and leave
    /// an empty file.
    /// </summary>
    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, JsonDefaults.Options);
            stream.Flush(flushToDisk: true);
        }
        File.Move(tmp, FilePath, overwrite: true);
    }
}
