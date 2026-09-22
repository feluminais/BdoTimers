using System.Collections.Concurrent;

namespace BdoTimers.Core.Scheduling;

/// <summary>Accepts both Windows ids and IANA ids (.NET resolves IANA ids on Windows via ICU).</summary>
public static class TimeZones
{
    static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new();

    public static TimeZoneInfo Find(string id) => Cache.GetOrAdd(id, TimeZoneInfo.FindSystemTimeZoneById);
}
