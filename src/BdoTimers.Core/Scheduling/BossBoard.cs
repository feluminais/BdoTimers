using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Scheduling;

/// <summary>Bosses spawning at the same instant; <see cref="Skipped"/> when every one of them is muted.</summary>
public sealed record SpawnGroup(DateTimeOffset AtUtc, IReadOnlyList<TimerDef> Bosses, bool Skipped);

public sealed record BossBoardState(SpawnGroup? Previous, SpawnGroup? Next, SpawnGroup? FollowedBy);

/// <summary>The previous, next and following spawn of the followed built-in bosses.</summary>
public static class BossBoard
{
    // A week plus a day either side: every boss spawns at least weekly, so previous and following always exist.
    internal static readonly TimeSpan Reach = TimeSpan.FromDays(8);

    public static BossBoardState Build(AppData data, DateTimeOffset now) =>
        At(Spawns(data, now - Reach, now + Reach, followedOnly: true), now);

    /// <summary>The board at <paramref name="now"/> from ascending groups that cover at least <see cref="Reach"/> either
    /// side of it; groups further away are left out, as <see cref="Build"/> leaves them out.</summary>
    internal static BossBoardState At(IReadOnlyList<SpawnGroup> groups, DateTimeOffset now)
    {
        var next = 0;
        while (next < groups.Count && groups[next].AtUtc <= now) next++;
        SpawnGroup? InReach(int i) =>
            i >= 0 && i < groups.Count && groups[i].AtUtc >= now - Reach && groups[i].AtUtc < now + Reach ? groups[i] : null;
        return new BossBoardState(InReach(next - 1), InReach(next), InReach(next + 1));
    }

    /// <summary>Built-in boss spawns in [fromUtc, toUtc), grouped by instant and ascending; Morning Light bosses first.</summary>
    internal static List<SpawnGroup> Spawns(AppData data, DateTimeOffset fromUtc, DateTimeOffset toUtc, bool followedOnly)
    {
        var muted = data.Muted.ToHashSet();
        return data.Timers
            .Where(t => BossRegions.IsSelected(data, t) && t.Scheduled is not null && (t.Enabled || !followedOnly))
            .SelectMany(t => ScheduleMath.From(t.Scheduled!, fromUtc).TakeWhile(at => at < toUtc).Select(at => (Boss: t, At: at)))
            .GroupBy(s => s.At)
            .OrderBy(g => g.Key)
            .Select(g => new SpawnGroup(
                g.Key,
                BossOrder.Sort(g.Select(s => s.Boss)).ToList(),
                g.All(s => muted.Contains(new MutedOccurrence(s.Boss.Id, g.Key)))))
            .ToList();
    }
}

/// <summary>
/// <see cref="BossBoard.Build"/> for callers that ask every second: the spawns are worked out once per data instance and
/// day, and the same state comes back until the board moves on. Thread-safe.
/// </summary>
public sealed class BossBoardCache
{
    static readonly TimeSpan Span = TimeSpan.FromDays(1);

    readonly object _gate = new();
    AppData? _data;
    DateTimeOffset _from;
    List<SpawnGroup> _groups = [];
    BossBoardState? _board;

    public BossBoardState Get(AppData data, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(data, _data) || now < _from || now > _from + Span)
            {
                _data = data;
                _from = now;
                // Reach either side of every instant up to Span from now.
                _groups = BossBoard.Spawns(data, now - BossBoard.Reach, now + Span + BossBoard.Reach, followedOnly: true);
                _board = null;
            }
            // The groups are the same instances while the data is, so an unmoved board compares equal.
            var board = BossBoard.At(_groups, now);
            if (_board is { } last && last == board) return last;
            return _board = board;
        }
    }
}
