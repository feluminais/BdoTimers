using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Tests;

public sealed class FailureTrackerTests
{
    readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Repeated_failure_updates_diagnostics_without_repeated_notices()
    {
        var tracker = new FailureTracker(_clock);
        Assert.True(tracker.Report("Voice", "Missing files"));
        _clock.UtcNow += TimeSpan.FromSeconds(1);
        Assert.False(tracker.Report("Voice", "Missing files"));
        var fault = Assert.Single(tracker.Active);
        Assert.Equal(2, fault.Count);
        Assert.Equal(_clock.UtcNow, fault.OccurredAt);
        _clock.UtcNow += TimeSpan.FromMinutes(5);
        Assert.True(tracker.Report("Voice", "Missing files"));
    }

    [Fact]
    public void Recovery_clears_only_its_channel_and_preserves_last_failure()
    {
        var tracker = new FailureTracker(_clock);
        tracker.Report("Voice", "Missing files");
        tracker.Report("Sound", "No output device");
        Assert.True(tracker.Clear("Voice"));
        Assert.False(tracker.Clear("Voice"));
        Assert.Equal("Sound", Assert.Single(tracker.Active).Area);
        tracker.Clear("Sound");
        Assert.Empty(tracker.Active);
        Assert.Equal("No output device", tracker.Latest!.Message);
        Assert.True(tracker.Report("Voice", "Missing files"));
        Assert.Equal(1, Assert.Single(tracker.Active).Count);
    }

    [Fact]
    public void Changed_failure_is_visible_immediately()
    {
        var tracker = new FailureTracker(_clock);
        tracker.Report("Voice", "Missing files");
        Assert.True(tracker.Report("Voice", "Could not load"));
        Assert.Equal("Could not load", Assert.Single(tracker.Active).Message);
    }

    [Fact]
    public void Concurrent_failures_do_not_lose_occurrences()
    {
        var tracker = new FailureTracker(_clock);
        Parallel.For(0, 100, _ => tracker.Report("Sound", "Unavailable"));
        Assert.Equal(100, Assert.Single(tracker.Active).Count);
    }
}
