using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Tests;

[Collection(nameof(Log))]
public sealed class RepeatingErrorLogTests : IDisposable
{
    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    readonly string _operation = $"Op{Guid.NewGuid():N}";
    readonly RepeatingErrorLog _errors;

    public RepeatingErrorLogTests()
    {
        Log.Init(_dir.Path, _clock);
        _errors = new RepeatingErrorLog(_operation, _clock);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Logs_the_first_failure_with_its_stack_and_hides_identical_repeats_for_an_hour()
    {
        _errors.Failed(Thrown(new IOException("locked")));
        for (var i = 0; i < 100; i++)
        {
            _clock.UtcNow += TimeSpan.FromSeconds(1);
            _errors.Failed(Thrown(new IOException("locked")));
        }

        var entry = Assert.Single(Entries());
        Assert.Contains($"ERR {_operation} failed", entry);
        Assert.Contains(nameof(Thrown), LogText());
    }

    [Fact]
    public void Reminds_once_an_hour_in_one_line_while_the_failure_lasts()
    {
        _errors.Failed(Thrown(new IOException("locked")));
        _clock.UtcNow += TimeSpan.FromMinutes(59);
        _errors.Failed(Thrown(new IOException("locked")));
        _clock.UtcNow += TimeSpan.FromMinutes(1);
        _errors.Failed(Thrown(new IOException("locked")));
        _clock.UtcNow += TimeSpan.FromMinutes(1);
        _errors.Failed(Thrown(new IOException("locked")));

        var entries = Entries();
        Assert.Equal(2, entries.Count);
        Assert.Contains($"ERR {_operation} still failing (2 times): locked", entries[1]);
        Assert.EndsWith(entries[1], LogText().TrimEnd());
    }

    [Fact]
    public void Logs_a_different_failure_in_full()
    {
        _errors.Failed(Thrown(new IOException("locked")));
        _errors.Failed(Thrown(new UnauthorizedAccessException("read-only")));

        Assert.Equal(2, Entries().Count);
        Assert.Contains("read-only", LogText());
    }

    [Fact]
    public void Logs_recovery_once_and_a_new_streak_in_full()
    {
        _errors.Succeeded();
        _errors.Failed(Thrown(new IOException("locked")));
        _errors.Succeeded();
        _errors.Succeeded();
        _errors.Failed(Thrown(new IOException("locked")));

        var entries = Entries();
        Assert.Equal(3, entries.Count);
        Assert.Contains($"INF {_operation} recovered", entries[1]);
        Assert.Contains($"ERR {_operation} failed", entries[2]);
    }

    static Exception Thrown(Exception ex)
    {
        try { throw ex; }
        catch (Exception caught) { return caught; }
    }

    string LogText() => string.Concat(Directory.GetFiles(_dir.Path, "*.log").Order().Select(File.ReadAllText));

    List<string> Entries() => LogText().Split(Environment.NewLine).Where(line => line.Contains(_operation)).ToList();
}
