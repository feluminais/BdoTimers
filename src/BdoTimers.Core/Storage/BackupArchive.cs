using System.IO.Compression;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Storage;

public sealed record BackupSnapshot(AppSettings Settings, AppData Timers, TodoData Todos);
public sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAtUtc, string AppVersion);
public sealed record PreparedRestore(string Directory, BackupManifest Manifest);

/// <summary>Versioned backups of personal data. Restore is prepared separately so live app writers can stop first.</summary>
public static class BackupArchive
{
    public const int FormatVersion = 1;
    const long MaximumBytes = 1024L * 1024 * 1024;
    const long MaximumJsonBytes = 16L * 1024 * 1024;
    const int MaximumEntries = 10000;
    const string ManifestName = "backup.json";
    static readonly string[] Required = [ManifestName, "settings.json", "timers.json", "todos.json"];

    public static void Export(string dataDirectory, string destination, BackupSnapshot snapshot, IClock clock, string appVersion)
    {
        if (Within(destination, dataDirectory)) throw new InvalidDataException("Save the backup outside the Data folder.");
        SavedDataValidation.Check(snapshot);
        CheckImages(dataDirectory, snapshot);
        var temporary = Path.GetFullPath(destination) + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var bytes = WriteJson(archive, ManifestName, new BackupManifest(FormatVersion, clock.UtcNow, appVersion))
                        + WriteJson(archive, "settings.json", snapshot.Settings)
                        + WriteJson(archive, "timers.json", snapshot.Timers)
                        + WriteJson(archive, "todos.json", snapshot.Todos);
                    var count = Required.Length;
                    foreach (var folder in new[] { "images", "sounds" })
                    {
                        var directory = Path.Combine(dataDirectory, folder);
                        if (!Directory.Exists(directory)) continue;
                        if (IsLink(directory)) throw new InvalidDataException("Linked data folders can't be backed up.");
                        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
                        {
                            var name = $"{folder}/{Path.GetFileName(file)}";
                            if (!Allowed(name)) continue;
                            if (IsLink(file)) throw new InvalidDataException("Linked data files can't be backed up.");
                            if (++count > MaximumEntries) throw new InvalidDataException("Too many files for one backup.");
                            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var output = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                            bytes += CopyLimited(input, output, MaximumBytes - bytes);
                        }
                    }
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>Extracts only known data files into a fresh sibling folder, then validates all saved models.</summary>
    public static PreparedRestore Prepare(string archivePath, string appDirectory)
    {
        var staging = Path.Combine(Path.GetFullPath(appDirectory), $".restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaximumEntries) throw new InvalidDataException("Too many files in the backup.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long bytes = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if ((name == "images/" || name == "sounds/") && entry.Length == 0) continue;
                if (!Allowed(name) || !names.Add(name)) throw new InvalidDataException("The backup contains an invalid or duplicate file.");
                var limit = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? MaximumJsonBytes : MaximumBytes - bytes;
                if (entry.Length > limit || entry.Length > MaximumBytes - bytes) throw new InvalidDataException("The backup is too large.");
                var file = Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                using var input = entry.Open();
                using var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                bytes += CopyLimited(input, output, Math.Min(limit, MaximumBytes - bytes));
            }
            if (Required.Any(n => !names.Contains(n))) throw new InvalidDataException("The backup is missing saved data.");
            return new(staging, ValidatePrepared(staging));
        }
        catch
        {
            Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    /// <summary>Swaps the prepared data into place and keeps the previous folder for recovery. A failed swap rolls back.</summary>
    public static string? ApplyPrepared(string stagingDirectory, string dataDirectory, IClock clock)
    {
        var staging = Path.GetFullPath(stagingDirectory);
        var data = Path.GetFullPath(dataDirectory);
        if (!string.Equals(Path.GetDirectoryName(staging), Path.GetDirectoryName(data), StringComparison.OrdinalIgnoreCase)
            || !IsRestoreFolder(staging)
            || IsLink(staging) || Directory.Exists(data) && IsLink(data))
            throw new InvalidDataException("The restore folder is invalid.");
        ValidatePrepared(staging);
        var previous = data + $".before-restore-{clock.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var hadData = Directory.Exists(data);
        if (hadData) Directory.Move(data, previous);
        try { Directory.Move(staging, data); }
        catch
        {
            if (hadData) Directory.Move(previous, data);
            throw;
        }
        // The manifest is harmless if its removal fails; it is not part of the app's saved state.
        try { File.Delete(Path.Combine(data, ManifestName)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return hadData ? previous : null;
    }

    public static void Discard(PreparedRestore restore)
    {
        if (!IsRestoreFolder(restore.Directory)) throw new InvalidDataException("The restore folder is invalid.");
        if (Directory.Exists(restore.Directory)) Directory.Delete(restore.Directory, recursive: true);
    }

    static BackupManifest ValidatePrepared(string directory)
    {
        if (IsLink(directory)) throw new InvalidDataException("The restore folder is invalid.");
        var files = Directory.EnumerateFiles(directory).ToList();
        foreach (var folder in Directory.EnumerateDirectories(directory))
        {
            if (Path.GetFileName(folder) is not ("images" or "sounds") || IsLink(folder)
                || Directory.EnumerateDirectories(folder).Any())
                throw new InvalidDataException("The restore folder contains an invalid folder.");
            files.AddRange(Directory.EnumerateFiles(folder));
        }
        if (files.Count > MaximumEntries || files.Any(IsLink) || files.Sum(f => new FileInfo(f).Length) > MaximumBytes)
            throw new InvalidDataException("The restore folder contains invalid files.");
        foreach (var file in files)
        {
            var name = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            if (!Allowed(name)) throw new InvalidDataException("The restore folder contains an invalid file.");
        }
        try
        {
            var manifest = ReadJson<BackupManifest>(directory, ManifestName);
            if (manifest.FormatVersion != FormatVersion) throw new InvalidDataException("This backup needs a different app version.");
            var snapshot = new BackupSnapshot(ReadJson<AppSettings>(directory, "settings.json"),
                ReadJson<AppData>(directory, "timers.json"), ReadJson<TodoData>(directory, "todos.json"));
            SavedDataValidation.Check(snapshot);
            CheckImages(directory, snapshot);
            return manifest;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("The backup contains unreadable saved data.", ex);
        }
    }

    static T ReadJson<T>(string directory, string name) where T : class
    {
        var file = new FileInfo(Path.Combine(directory, name));
        if (!file.Exists || file.Length > MaximumJsonBytes) throw new InvalidDataException("The backup is missing valid saved data.");
        using var stream = file.OpenRead();
        return JsonSerializer.Deserialize<T>(stream, JsonDefaults.Options) ?? throw new InvalidDataException("The backup contains empty saved data.");
    }

    static long WriteJson<T>(ZipArchive archive, string name, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Options);
        if (bytes.Length > MaximumJsonBytes) throw new InvalidDataException("The saved data is too large for one backup.");
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(bytes);
        return bytes.Length;
    }

    static long CopyLimited(Stream input, Stream output, long maximum)
    {
        var buffer = new byte[81920];
        long count = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            count += read;
            if (count > maximum) throw new InvalidDataException("The backup is too large.");
            output.Write(buffer, 0, read);
        }
        return count;
    }

    static bool Allowed(string name)
    {
        if (Required.Contains(name, StringComparer.Ordinal)) return true;
        var parts = name.Split('/');
        return parts.Length == 2 && SafeFileName(parts[1]) && (parts[0] switch
        {
            // WPF decodes pictures by content; imported files retain their original extension, including none.
            "images" => true,
            "sounds" => new[] { ".wav", ".mp3" }.Contains(Path.GetExtension(parts[1]), StringComparer.OrdinalIgnoreCase),
            _ => false,
        });
    }

    static void CheckImages(string directory, BackupSnapshot snapshot)
    {
        var referenced = snapshot.Timers.Timers.Select(t => t.ImageFile).Append(snapshot.Settings.Overlay.BackgroundImage)
            .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in referenced)
            if (!File.Exists(Path.Combine(directory, "images", name)))
                throw new InvalidDataException($"The saved picture {name} is missing.");
    }

    internal static bool SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ')
            || name.Contains('\\') || name.Contains('/') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        var stem = name.Split('.')[0].ToUpperInvariant();
        return stem is not ("CON" or "PRN" or "AUX" or "NUL") && !Enumerable.Range(1, 9).Any(n => stem == $"COM{n}" || stem == $"LPT{n}");
    }

    static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    static bool IsRestoreFolder(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.StartsWith(".restore-", StringComparison.Ordinal) && Guid.TryParseExact(name[9..], "N", out _);
    }
    static bool Within(string file, string directory) => Path.GetFullPath(file).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
