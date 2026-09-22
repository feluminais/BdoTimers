using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Tests;

public class LogTests
{
    [Fact]
    public void Writes_errors_with_exception_to_daily_file()
    {
        using var dir = new TempDir();
        Log.Init(dir.Path);

        Log.Error("sound failed", new InvalidOperationException("no device"));

        var text = File.ReadAllText(Directory.GetFiles(dir.Path, "*.log").Single());
        Assert.Contains("ERR sound failed", text);
        Assert.Contains("no device", text);
    }
}
