using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>Land of the Morning Light bosses lead shared spawns and boss lists.</summary>
public static class BossOrder
{
    static readonly HashSet<string> MorningLight = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bulgasal", "Golden Pig King", "Sangoon", "Uturi",
    };

    public static int Priority(TimerDef timer) => timer.IsBuiltIn && MorningLight.Contains(timer.Name) ? 0 : 1;

    public static IOrderedEnumerable<TimerDef> Sort(IEnumerable<TimerDef> timers) =>
        timers.OrderBy(Priority).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
}
