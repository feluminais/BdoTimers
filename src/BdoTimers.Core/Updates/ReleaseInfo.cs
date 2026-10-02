namespace BdoTimers.Core.Updates;

public sealed record ReleaseInfo(Version Version, Uri Page)
{
    public string Number => Version.ToString(Version.Revision == 0 ? 3 : 4);
}

public interface IReleaseSource
{
    Task<ReleaseInfo?> LatestStableAsync(CancellationToken cancel);
}

public enum UpdateStatus { Idle, Checking, UpToDate, UpdateAvailable, CouldNotCheck }
public sealed record UpdateResult(UpdateStatus Status, ReleaseInfo? Release = null);
public sealed record UpdateCheckState
{
    public DateTimeOffset? LastAttemptUtc { get; init; }
}
