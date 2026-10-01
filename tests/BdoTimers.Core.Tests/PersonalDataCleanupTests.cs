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
}
