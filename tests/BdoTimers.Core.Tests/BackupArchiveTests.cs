using System.IO.Compression;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class BackupArchiveTests
{
    static readonly FakeClock Clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    static BackupSnapshot Snapshot => new(new AppSettings { Volume = 0.3f },
        new AppData { Timers = [new TimerDef { Name = "My timer", Kind = TimerKind.Countdown, Countdown = new() }], DataVersion = DataMigrations.Current },
        new TodoData { Lists = [new TodoList { Name = "My list", Rows = [new TodoRow { Text = "My task", Done = true }] }] });

    [Fact]
    public void Round_trip_keeps_personal_state_and_media_and_excludes_caches()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        Directory.CreateDirectory(Path.Combine(data, "images"));
        Directory.CreateDirectory(Path.Combine(data, "sounds"));
        Directory.CreateDirectory(Path.Combine(data, "speech"));
        File.WriteAllBytes(Path.Combine(data, "images", "custom.jpg"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(data, "sounds", "custom.wav"), [4, 5, 6]);
        File.WriteAllText(Path.Combine(data, "speech", "cached.wav"), "cache");
        var snapshot = Snapshot;
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, snapshot, Clock, "1.0.123");

        var prepared = BackupArchive.Prepare(archive, temp.Path);

        Assert.Equal(Clock.UtcNow, prepared.Manifest.CreatedAtUtc);
        Assert.Equal("1.0.123", prepared.Manifest.AppVersion);
        Assert.Equal(0.3f, Read<AppSettings>(prepared.Directory, "settings.json").Volume);
        Assert.Equal(snapshot.Timers.Timers[0].Id, Read<AppData>(prepared.Directory, "timers.json").Timers[0].Id);
        Assert.True(Read<TodoData>(prepared.Directory, "todos.json").Lists[0].Rows[0].Done);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(prepared.Directory, "images", "custom.jpg")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(prepared.Directory, "sounds", "custom.wav")));
        Assert.False(Directory.Exists(Path.Combine(prepared.Directory, "speech")));
    }

    [Fact]
    public void Preparing_leaves_current_data_intact_and_applying_keeps_a_recovery_folder()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        File.WriteAllText(Path.Combine(data, "original.txt"), "old data");
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        var prepared = BackupArchive.Prepare(archive, temp.Path);
        Assert.Equal("old data", File.ReadAllText(Path.Combine(data, "original.txt")));

        var previous = BackupArchive.ApplyPrepared(prepared.Directory, data, Clock);

        Assert.Equal("old data", File.ReadAllText(Path.Combine(previous!, "original.txt")));
        Assert.Equal("My timer", Read<AppData>(data, "timers.json").Timers[0].Name);
        Assert.False(File.Exists(Path.Combine(data, "backup.json")));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("images/../../outside.jpg")]
    [InlineData("images\\outside.jpg")]
    [InlineData("images/CON.jpg")]
    [InlineData("images/picture.jpg ")]
    [InlineData("run.exe")]
    [InlineData("SETTINGS.JSON")]
    public void Invalid_entries_are_rejected_without_touching_data(string name)
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        File.WriteAllText(Path.Combine(data, "original.txt"), "kept");
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("bad");

        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(data, "original.txt")));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".restore-*"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"timers\":null}")]
    [InlineData("{\"dataVersion\":999}")]
    [InlineData("{ broken")]
    public void Invalid_saved_models_are_rejected(string json)
    {
        using var temp = new TempDir();
        var archive = temp.File("backup.zip");
        BackupArchive.Export(temp.File("Data"), archive, Snapshot, Clock, "1.0.0");
        Replace(archive, "timers.json", json);
        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
    }

    [Fact]
    public void Unsupported_format_or_missing_state_is_rejected()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        Replace(archive, "backup.json", "{\"formatVersion\":999}");
        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) zip.GetEntry("todos.json")!.Delete();
        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
    }

    [Fact]
    public void A_damaged_prepared_restore_is_rechecked_before_replacing_data()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        File.WriteAllText(Path.Combine(data, "original.txt"), "kept");
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        var prepared = BackupArchive.Prepare(archive, temp.Path);
        File.WriteAllText(Path.Combine(prepared.Directory, "settings.json"), "null");
        Assert.Throws<InvalidDataException>(() => BackupArchive.ApplyPrepared(prepared.Directory, data, Clock));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(data, "original.txt")));
        Assert.Empty(Directory.GetDirectories(temp.Path, "Data.before-restore-*"));
    }

    [Fact]
    public void Export_inside_data_is_rejected_and_existing_backup_survives_failed_export()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        Assert.Throws<InvalidDataException>(() => BackupArchive.Export(data, Path.Combine(data, "backup.zip"), Snapshot, Clock, "1"));
        var archive = temp.File("backup.zip");
        File.WriteAllText(archive, "previous backup");
        var invalid = Snapshot with { Timers = new AppData { DataVersion = 999 } };
        Assert.Throws<InvalidDataException>(() => BackupArchive.Export(data, archive, invalid, Clock, "1"));
        Assert.Equal("previous backup", File.ReadAllText(archive));
    }

    [Fact]
    public void Failed_directory_swap_rolls_back_the_current_data()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        File.WriteAllText(Path.Combine(data, "original.txt"), "kept");
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, Snapshot, Clock, "1.0.0");
        var prepared = BackupArchive.Prepare(archive, temp.Path);
        using var held = new FileStream(Path.Combine(prepared.Directory, "timers.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<IOException>(() => BackupArchive.ApplyPrepared(prepared.Directory, data, Clock));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(data, "original.txt")));
        Assert.Empty(Directory.GetDirectories(temp.Path, "Data.before-restore-*"));
    }

    static T Read<T>(string directory, string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name)), JsonDefaults.Options)!;
    static void Replace(string archive, string name, string content)
    {
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
        zip.GetEntry(name)!.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
