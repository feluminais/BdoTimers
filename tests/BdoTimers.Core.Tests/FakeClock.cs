using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
}
