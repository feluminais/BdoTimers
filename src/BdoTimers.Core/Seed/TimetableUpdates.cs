using System.Security.Cryptography;
using System.Text;
using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

public sealed record TimetableChange(string Name, ScheduledSpec? Current, ScheduledSpec? Replacement, bool HasCustomTimes);

public sealed record TimetableReview(bool NeedsReview, IReadOnlyList<TimetableChange> Changes);

/// <summary>Compares bundled schedule versions while keeping personal spawn times and alert choices explicit.</summary>
public static class TimetableUpdates
{
    public static string Revision(BossSeed seed)
    {
        var lines = SeedService.ToTimers(seed, new AlertConfig())
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => $"{t.Name.ToUpperInvariant()}|{t.Scheduled!.TimeZoneId}|{SlotsKey(t.Scheduled)}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    public static AppData InitializeBaseline(AppData data, BossSeed seed)
    {
        if (data.AcceptedBossTimetable is not null) return data;
        var current = Bosses(data);
        var bundled = Bundled(seed);
        return current.Count == bundled.Count && bundled.All(b => current.TryGetValue(b.Key, out var timer)
            && Same(timer.Scheduled, b.Value.Scheduled)) ? data with { AcceptedBossTimetable = seed } : data;
    }

    public static TimetableReview Review(AppData data, BossSeed seed)
    {
        var baseline = data.AcceptedBossTimetable;
        if (baseline is not null && Revision(baseline) == Revision(seed)) return new(false, []);
        var current = Bosses(data);
        var target = Bundled(seed);
        var previous = baseline is null ? null : Bundled(baseline);
        var names = (previous?.Keys ?? current.Keys).Union(target.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var changes = new List<TimetableChange>();
        foreach (var name in names)
        {
            current.TryGetValue(name, out var saved);
            target.TryGetValue(name, out var fresh);
            TimerDef? old = null;
            previous?.TryGetValue(name, out old);
            // A personal edit to an unchanged bundled boss is preserved without making it an update candidate.
            if (previous is not null && Same(old?.Scheduled, fresh?.Scheduled)) continue;
            if (Same(saved?.Scheduled, fresh?.Scheduled)) continue;
            var custom = saved is not null && (previous is null || !Same(saved.Scheduled, old?.Scheduled));
            changes.Add(new(fresh?.Name ?? saved?.Name ?? name, saved?.Scheduled, fresh?.Scheduled, custom));
        }
        return new(true, changes);
    }

    public static AppData Apply(AppData data, BossSeed seed, IEnumerable<string> selectedNames)
    {
        var review = Review(data, seed);
        var selected = selectedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changes = review.Changes.Where(c => selected.Contains(c.Name)).ToList();
        var timers = data.Timers.ToList();
        foreach (var change in changes)
        {
            var existing = timers.FirstOrDefault(t => t.IsBuiltIn && string.Equals(t.Name, change.Name, StringComparison.OrdinalIgnoreCase));
            if (change.Replacement is null)
            {
                timers.RemoveAll(t => t.IsBuiltIn && string.Equals(t.Name, change.Name, StringComparison.OrdinalIgnoreCase));
            }
            else if (existing is not null)
            {
                var index = timers.IndexOf(existing);
                timers[index] = existing with { Name = change.Name, Scheduled = change.Replacement };
            }
            else
            {
                timers.Add(new TimerDef { Name = change.Name, Kind = TimerKind.Scheduled, IsBuiltIn = true, Scheduled = change.Replacement });
            }
        }
        var ids = timers.Select(t => t.Id).ToHashSet();
        return data with { Timers = timers, Muted = data.Muted.Where(m => ids.Contains(m.TimerId)).ToList(), AcceptedBossTimetable = seed };
    }

    public static bool Same(ScheduledSpec? left, ScheduledSpec? right) => left is null || right is null
        ? left is null && right is null
        : left.TimeZoneId == right.TimeZoneId && SlotsKey(left) == SlotsKey(right);

    static string SlotsKey(ScheduledSpec schedule) => string.Join(',', schedule.Slots.Distinct()
        .OrderBy(s => s.Day).ThenBy(s => s.Time).Select(s => $"{(int)s.Day}:{s.Time:HH:mm}"));

    static Dictionary<string, TimerDef> Bosses(AppData data) => data.Timers.Where(t => t.IsBuiltIn)
        .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    static Dictionary<string, TimerDef> Bundled(BossSeed seed) => SeedService.ToTimers(seed, new AlertConfig())
        .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
}
