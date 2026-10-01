using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

/// <summary>Log is static, so tests that point it at their own folder can't run in parallel.</summary>
[Collection(nameof(Log))]
public class LogTests
{
    [Fact]
    public void Writes_errors_with_exception_to_daily_file()
    {
        using var dir = new TempDir();
        Log.Init(dir.Path, new SystemClock());

        Log.Error("sound failed", new InvalidOperationException("no device"));

        var text = File.ReadAllText(Directory.GetFiles(dir.Path, "*.log").Single());
        Assert.Contains("ERR sound failed", text);
        Assert.Contains("no device", text);
    }

    [Fact]
    public void Init_deletes_logs_older_than_the_retention_by_the_clock()
    {
        using var dir = new TempDir();
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var old = dir.File("2026-09-10.log");
        var recent = dir.File("2026-09-25.log");
        File.WriteAllText(old, "");
        File.WriteAllText(recent, "");
        File.SetLastWriteTimeUtc(old, now.AddDays(-15).UtcDateTime);
        File.SetLastWriteTimeUtc(recent, now.AddDays(-6).UtcDateTime);

        Log.Init(dir.Path, new FakeClock(now), keepDays: 14);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }
}
