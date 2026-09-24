using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>Bosses spawning at the same instant; <see cref="Skipped"/> when every one of them is muted.</summary>
public sealed record SpawnGroup(DateTimeOffset AtUtc, IReadOnlyList<TimerDef> Bosses, bool Skipped);

public sealed record BossBoardState(SpawnGroup? Previous, SpawnGroup? Next, SpawnGroup? FollowedBy);

/// <summary>The previous, next and following spawn of the followed built-in bosses.</summary>
public static class BossBoard
{
    // A week plus a day either side: every boss spawns at least weekly, so previous and following always exist.
    static readonly TimeSpan Reach = TimeSpan.FromDays(8);

    public static BossBoardState Build(AppData data, DateTimeOffset now)
    {
        var groups = Spawns(data, now - Reach, now + Reach, followedOnly: true);
        var next = groups.FindIndex(g => g.AtUtc > now);
        var previousIndex = next < 0 ? groups.Count - 1 : next - 1;
        return new BossBoardState(
            previousIndex >= 0 ? groups[previousIndex] : null,
            next >= 0 ? groups[next] : null,
            next >= 0 && next + 1 < groups.Count ? groups[next + 1] : null);
    }

    /// <summary>Built-in boss spawns in [fromUtc, toUtc), grouped by instant and ascending; bosses sorted by name.</summary>
    internal static List<SpawnGroup> Spawns(AppData data, DateTimeOffset fromUtc, DateTimeOffset toUtc, bool followedOnly)
    {
        var muted = data.Muted.ToHashSet();
        return data.Timers
            .Where(t => t.IsBuiltIn && t.Scheduled is not null && (t.Enabled || !followedOnly))
            .SelectMany(t => ScheduleMath.From(t.Scheduled!, fromUtc).TakeWhile(at => at < toUtc).Select(at => (Boss: t, At: at)))
            .GroupBy(s => s.At)
            .OrderBy(g => g.Key)
            .Select(g => new SpawnGroup(
                g.Key,
                g.Select(s => s.Boss).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                g.All(s => muted.Contains(new MutedOccurrence(s.Boss.Id, g.Key)))))
            .ToList();
    }
}
