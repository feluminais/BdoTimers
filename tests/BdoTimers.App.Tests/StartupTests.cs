using System.IO;

namespace BdoTimers.App.Tests;

public sealed class StartupTests
{
    [Fact]
    public void Argument_is_the_value_after_its_name()
    {
        string[] args = ["--minimized", "--performance-report", "report.txt", "--wait-for-exit"];
        Assert.Equal("report.txt", App.Argument(args, "--performance-report"));
        Assert.Null(App.Argument(args, "--restore-prepared"));
    }

    [Fact]
    public void Argument_without_a_value_is_null()
    {
        Assert.Null(App.Argument(["--wait-for-exit"], "--wait-for-exit"));
    }

    [Fact]
    public void Data_folder_is_writable_when_it_can_be_created_and_not_when_a_file_is_in_the_way()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bdo-startup-{Guid.NewGuid():N}");
        try
        {
            var data = Path.Combine(root, "Data");
            Assert.True(App.CanWrite(data));
            Assert.True(Directory.Exists(data));
            Assert.Empty(Directory.GetFileSystemEntries(data));

            var blocked = Path.Combine(root, "file");
            File.WriteAllText(blocked, "");
            Assert.False(App.CanWrite(blocked));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
