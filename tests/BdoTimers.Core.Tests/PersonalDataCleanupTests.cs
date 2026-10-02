using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.SetupUi;

namespace BdoTimers.Core.Tests;

public class PersonalDataCleanupTests
{
    [Fact]
    public void Explicit_deletion_removes_data_and_owned_restore_folders_and_keeps_other_folders()
    {
        using var temp = new TempDir();
        foreach (var folder in new[] { "Data", "Data.before-restore-20261002-120000-0123456789abcdef0123456789abcdef",
            ".restore-0123456789abcdef0123456789abcdef", "Data.before-restore-my-files", ".restore-my-files", "Other" })
        {
            Directory.CreateDirectory(temp.File(folder));
            File.WriteAllText(temp.File(Path.Combine(folder, "file.txt")), "data");
        }
        File.SetAttributes(temp.File("Data/file.txt"), FileAttributes.ReadOnly);

        PersonalDataCleanup.Delete(temp.Path);

        Assert.False(Directory.Exists(temp.File("Data")));
        Assert.False(Directory.Exists(temp.File("Data.before-restore-20261002-120000-0123456789abcdef0123456789abcdef")));
        Assert.False(Directory.Exists(temp.File(".restore-0123456789abcdef0123456789abcdef")));
        Assert.True(File.Exists(temp.File("Data.before-restore-my-files/file.txt")));
        Assert.True(File.Exists(temp.File(".restore-my-files/file.txt")));
        Assert.True(File.Exists(temp.File("Other/file.txt")));
    }

    [Fact]
    public void Explicit_deletion_removes_the_folders_backup_restore_creates()
    {
        using var temp = new TempDir();
        var app = Directory.CreateDirectory(temp.File("BdoTimers")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(app, "Data")).FullName;
        var archive = temp.File("backup.zip");
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
        BackupArchive.Export(data, archive,
            new BackupSnapshot(new AppSettings(), new AppData { DataVersion = DataMigrations.Current }, new TodoData()),
            clock, "1.0.0");
        var previous = BackupArchive.ApplyPrepared(BackupArchive.Prepare(archive, app).Directory, data, clock);
        var pending = BackupArchive.Prepare(archive, app).Directory;

        PersonalDataCleanup.Delete(app);

        Assert.False(Directory.Exists(data));
        Assert.False(Directory.Exists(previous));
        Assert.False(Directory.Exists(pending));
    }
}
