using System.Globalization;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;

namespace BdoTimers.Core.Storage;

public sealed record LoadResult<T>(T Value, string? RecoveredBackupPath);

public sealed class JsonFileStore<T>(string filePath, Func<T> createDefault) where T : class
{
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Invalid JSON or saved models are renamed to "*.bad-&lt;timestamp&gt;" and replaced with defaults.
    /// Newer saved formats and filesystem failures propagate without changing the file.
    /// </summary>
    public LoadResult<T> Load()
    {
        if (!File.Exists(FilePath)) return new(createDefault(), null);
        try
        {
            var json = File.ReadAllText(FilePath);
            using var document = JsonDocument.Parse(json);
            CheckVersion(document.RootElement);
            var value = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
            if (value is not null)
            {
                SavedDataValidation.Check(value);
                return new(value, null);
            }
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        catch (InvalidDataException) { }

        // Numbered when one from the same second exists (or the autumn clock change repeats an hour), so none is lost.
        var stamped = $"{FilePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
        var backup = stamped;
        for (var n = 2; File.Exists(backup); n++) backup = $"{stamped}-{n}";
        File.Move(FilePath, backup);
        return new(createDefault(), backup);
    }

    void CheckVersion(JsonElement root)
    {
        var version = typeof(T) == typeof(AppData) ? (Name: "dataVersion", Current: DataMigrations.Current)
            : typeof(T) == typeof(TodoData) ? (Name: "defaultsVersion", Current: TodoData.CurrentDefaultsVersion)
            : (Name: "", Current: 0);
        if (version.Name.Length == 0 || root.ValueKind != JsonValueKind.Object) return;
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(version.Name, StringComparison.OrdinalIgnoreCase)
                && IsNewer(property.Value, version.Current))
                throw new UnsupportedDataVersionException(FilePath);
    }

    static bool IsNewer(JsonElement version, int current) => version.ValueKind switch
    {
        JsonValueKind.Number => version.GetDouble() > current,
        JsonValueKind.String => long.TryParse(version.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            && number > current,
        _ => false,
    };

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

public sealed class UnsupportedDataVersionException(string filePath)
    : IOException($"{Path.GetFileName(filePath)} needs a newer version of BDO Timers. Install the latest version to open it. Your saved file was left unchanged.")
{
    public string FilePath { get; } = filePath;
}
