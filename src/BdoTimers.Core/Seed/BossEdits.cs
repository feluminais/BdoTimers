using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

/// <summary>
/// Bosses the player adds to a region. Boss names are unique within a region, since timetable updates and resets match
/// bosses by name.
/// </summary>
public static class BossEdits
{
    public const string NewBossName = "New boss";

    /// <summary>The bosses of <paramref name="regionId"/>, bundled and added.</summary>
    public static IEnumerable<TimerDef> Of(AppData data, string regionId) =>
        data.Timers.Where(t => t.IsBuiltIn && BossRegions.RegionOf(t) == regionId);

    /// <summary>Whether no boss of the region but <paramref name="except"/> has <paramref name="name"/>, ignoring case.</summary>
    public static bool IsNameFree(AppData data, string regionId, string name, Guid? except = null) =>
        Of(data, regionId).All(t => t.Id == except || !string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A boss for the selected region under the first free "New boss" name, spawning Monday 20:00 server time.</summary>
    public static TimerDef Create(AppData data)
    {
        var region = BossRegions.Find(data.SelectedBossRegion);
        var name = Enumerable.Range(1, int.MaxValue).Select(n => n == 1 ? NewBossName : $"{NewBossName} {n}")
            .First(n => IsNameFree(data, region.Id, n));
        return Create(data, name, [new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))]);
    }

    /// <summary>A boss for the selected region with these slots in its server time; dates limit it to an event's run.</summary>
    public static TimerDef Create(AppData data, string name, IReadOnlyList<Slot> slots, DateOnly? start = null, DateOnly? end = null)
    {
        var region = BossRegions.Find(data.SelectedBossRegion);
        return new TimerDef
        {
            Name = name,
            Kind = TimerKind.Scheduled,
            IsBuiltIn = true,
            AddedByUser = true,
            BossRegionId = region.Id,
            Scheduled = new ScheduledSpec { TimeZoneId = region.TimeZoneId, Slots = slots, StartDate = start, EndDate = end },
        };
    }
}
