using System.IO.Compression;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Seed;

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
    [InlineData(".jfif")]
    [InlineData(".dat")]
    [InlineData("")]
    public void Imported_pictures_round_trip_regardless_of_their_original_extension(string extension)
    {
        using var temp = new TempDir();
        var images = Directory.CreateDirectory(temp.File("Data/images")).FullName;
        var name = "picture" + extension;
        File.WriteAllBytes(Path.Combine(images, name), [1, 2, 3]);
        var snapshot = Snapshot;
        snapshot = snapshot with
        {
            Timers = snapshot.Timers with { Timers = [snapshot.Timers.Timers[0] with { ImageFile = name }] },
            Settings = snapshot.Settings with { Overlay = snapshot.Settings.Overlay with { BackgroundImage = name } },
        };
        var archive = temp.File("backup.zip");

        BackupArchive.Export(temp.File("Data"), archive, snapshot, Clock, "1");
        var prepared = BackupArchive.Prepare(archive, temp.Path);

        Assert.Equal(name, Read<AppData>(prepared.Directory, "timers.json").Timers[0].ImageFile);
        Assert.Equal(name, Read<AppSettings>(prepared.Directory, "settings.json").Overlay.BackgroundImage);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(prepared.Directory, "images", name)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_referenced_pictures_fail_export_without_replacing_an_existing_backup(bool overlay)
    {
        using var temp = new TempDir();
        var snapshot = Snapshot;
        snapshot = overlay
            ? snapshot with { Settings = snapshot.Settings with { Overlay = snapshot.Settings.Overlay with { BackgroundImage = "missing.jpg" } } }
            : snapshot with { Timers = snapshot.Timers with { Timers = [snapshot.Timers.Timers[0] with { ImageFile = "missing.jpg" }] } };
        var archive = temp.File("backup.zip");
        File.WriteAllText(archive, "previous backup");

        Assert.Throws<InvalidDataException>(() => BackupArchive.Export(temp.File("Data"), archive, snapshot, Clock, "1"));

        Assert.Equal("previous backup", File.ReadAllText(archive));
    }

    [Fact]
    public void Missing_referenced_picture_in_an_archive_is_rejected_before_restore()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.File("Data/images"));
        File.WriteAllBytes(temp.File("Data/images/picture.jpg"), [1, 2, 3]);
        var snapshot = Snapshot;
        snapshot = snapshot with { Timers = snapshot.Timers with { Timers = [snapshot.Timers.Timers[0] with { ImageFile = "picture.jpg" }] } };
        var archive = temp.File("backup.zip");
        BackupArchive.Export(temp.File("Data"), archive, snapshot, Clock, "1");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) zip.GetEntry("images/picture.jpg")!.Delete();

        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(temp.File("Data/images/picture.jpg")));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".restore-*"));
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

    [Theory]
    [InlineData("selection")]
    [InlineData("profile")]
    [InlineData("baseline")]
    [InlineData("duplicate")]
    [InlineData("timer")]
    public void Invalid_region_data_is_rejected_before_export_or_restore(string invalidPart)
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        var archive = temp.File("backup.zip");
        var timers = Snapshot.Timers with
        {
            BossRegions = [new() { RegionId = "eu", SeedApplied = true }],
        };
        timers = invalidPart switch
        {
            "selection" => timers with { SelectedBossRegion = "unknown" },
            "profile" => timers with { BossRegions = [new() { RegionId = "unknown" }] },
            "baseline" => timers with { BossRegions = [new() { RegionId = "na", AcceptedBossTimetable = new("Nowhere/Missing", []) }] },
            "duplicate" => timers with { BossRegions = [timers.BossRegions[0], timers.BossRegions[0]] },
            _ => timers with { Timers = [new() { IsBuiltIn = true, BossRegionId = "unknown", Kind = TimerKind.Scheduled,
                Scheduled = new() { TimeZoneId = "UTC" } }] },
        };
        var invalid = Snapshot with { Timers = timers };
        Assert.Throws<InvalidDataException>(() => BackupArchive.Export(data, archive, invalid, Clock, "1"));
        BackupArchive.Export(data, archive, Snapshot, Clock, "1");
        Replace(archive, "timers.json", JsonSerializer.Serialize(timers, JsonDefaults.Options));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
    }

    [Fact]
    public void Backup_round_trip_preserves_both_regions_and_selection()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        var initial = SeedService.ApplyIfNeeded(DataMigrations.Apply(new(), new()), SeedService.LoadEmbedded(), new());
        var store = new TimerStore(new(temp.File("timers.json"), () => new()), initial);
        store.SelectBossRegion("na", Clock);
        var na = store.Current.Timers.First(t => t.BossRegionId == "na");
        store.Modify(na.Id, t => t with { Enabled = false, Alerts = new() { LeadTimesMinutes = [31] } });
        var snapshot = Snapshot with { Timers = store.Current };
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, snapshot, Clock, "1");
        var prepared = BackupArchive.Prepare(archive, temp.Path);
        var loaded = Read<AppData>(prepared.Directory, "timers.json");
        Assert.Equal("na", loaded.SelectedBossRegion);
        Assert.Equal(store.Current.BossSelectionVersion, loaded.BossSelectionVersion);
        Assert.Equal(Clock.UtcNow, loaded.BossAlertsAfterUtc);
        Assert.Equal(2, loaded.BossRegions.Count);
        Assert.Equal(26, loaded.Timers.Count(t => t.IsBuiltIn));
        Assert.False(loaded.Timers.Single(t => t.Id == na.Id).Enabled);
        Assert.Equal([31], loaded.Timers.Single(t => t.Id == na.Id).Alerts.LeadTimesMinutes);
        Assert.False(TimetableUpdates.Review(loaded, SeedService.LoadEmbedded("na")).NeedsReview);
        Assert.False(TimetableUpdates.Review(loaded, SeedService.LoadEmbedded(), "eu").NeedsReview);
    }

    static T Read<T>(string directory, string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name)), JsonDefaults.Options)!;

    [Fact]
    public void Dated_timers_survive_backup_restore_with_finished_state_and_date_limits()
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        var dated = new TimerDef { Kind = TimerKind.OneTime, Name = "Event", OneTime = new()
        {
            Date = new(2026, 10, 2), Time = new(14, 0), TimeZoneId = "Europe/Berlin", Finished = true,
        } };
        var weekly = new TimerDef { Kind = TimerKind.Scheduled, Name = "Weekly", Scheduled = new()
        {
            TimeZoneId = "America/Los_Angeles", Slots = [new(DayOfWeek.Friday, new(19, 0))],
            StartDate = new(2026, 10, 2), EndDate = new(2026, 10, 30),
        } };
        var snapshot = Snapshot with { Timers = Snapshot.Timers with { Timers = [dated, weekly] } };
        var archive = temp.File("backup.zip");
        BackupArchive.Export(data, archive, snapshot, Clock, "1");
        var prepared = BackupArchive.Prepare(archive, temp.Path);
        BackupArchive.ApplyPrepared(prepared.Directory, data, Clock);
        var back = Read<AppData>(data, "timers.json");
        Assert.Equal(dated.OneTime, back.Timers[0].OneTime);
        Assert.Equal(weekly.Scheduled!.StartDate, back.Timers[1].Scheduled!.StartDate);
        Assert.Equal(weekly.Scheduled.EndDate, back.Timers[1].Scheduled!.EndDate);
    }

    [Theory]
    [InlineData("range")]
    [InlineData("missingEvent")]
    [InlineData("eventZone")]
    public void Invalid_dated_timer_data_is_rejected_before_export_or_restore(string part)
    {
        using var temp = new TempDir();
        var data = Directory.CreateDirectory(temp.File("Data")).FullName;
        var timer = part switch
        {
            "range" => new TimerDef { Kind = TimerKind.Scheduled, Scheduled = new()
                { StartDate = new(2026, 10, 3), EndDate = new(2026, 10, 2) } },
            "eventZone" => new TimerDef { Kind = TimerKind.OneTime, OneTime = new()
                { Date = new(2026, 10, 2), TimeZoneId = "Nowhere/Missing" } },
            _ => new TimerDef { Kind = TimerKind.OneTime },
        };
        var invalid = Snapshot with { Timers = Snapshot.Timers with { Timers = [timer] } };
        var archive = temp.File("backup.zip");
        Assert.Throws<InvalidDataException>(() => BackupArchive.Export(data, archive, invalid, Clock, "1"));
        BackupArchive.Export(data, archive, Snapshot, Clock, "1");
        Replace(archive, "timers.json", JsonSerializer.Serialize(invalid.Timers, JsonDefaults.Options));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Prepare(archive, temp.Path));
    }
    static void Replace(string archive, string name, string content)
    {
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
        zip.GetEntry(name)!.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
