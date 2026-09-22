using System.Text.Json;
using BdoTimers.Core.Json;

namespace BdoTimers.Core.Storage;

public sealed record LoadResult<T>(T Value, string? RecoveredBackupPath);

public sealed class JsonFileStore<T>(string filePath, Func<T> createDefault) where T : class
{
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Loads the file. An unreadable file is renamed to "*.bad-&lt;timestamp&gt;" and defaults are
    /// returned, so a damaged file never stops the app from starting.
    /// </summary>
    public LoadResult<T> Load()
    {
        if (!File.Exists(FilePath)) return new(createDefault(), null);
        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath), JsonDefaults.Options);
            if (value is not null) return new(value, null);
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }

        var backup = $"{FilePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(FilePath, backup, overwrite: true);
        return new(createDefault(), backup);
    }

    /// <summary>Writes a temp file then renames it over the target, so a crash mid-write keeps the old file.</summary>
    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonDefaults.Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
