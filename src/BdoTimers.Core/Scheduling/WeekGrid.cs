using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public enum CellState { Upcoming, Next, Past, Skipped, Unfollowed }

public sealed record GridEntry(TimerDef Boss, DateTimeOffset AtUtc, CellState State);

/// <summary>One local spawn time; <see cref="Days"/> holds Monday to Sunday, each with Morning Light bosses first.</summary>
public sealed record GridRow(TimeOnly Time, IReadOnlyList<IReadOnlyList<GridEntry>> Days);

public sealed record WeekGridState(DateOnly WeekStart, IReadOnlyList<GridRow> Rows);

/// <summary>The current local week (Monday to Sunday) of built-in boss spawns, one row per local spawn time.</summary>
public static class WeekGrid
{
    /// <param name="boards">Shares the boss board with other callers; without it the board is built afresh.</param>
    public static WeekGridState Build(AppData data, DateTimeOffset now, TimeZoneInfo local, BossBoardCache? boards = null)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, local).DateTime);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var fromUtc = ScheduleMath.LocalToUtc(weekStart.ToDateTime(TimeOnly.MinValue), local);
        var toUtc = ScheduleMath.LocalToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue), local);
        var next = (boards?.Get(data, now) ?? BossBoard.Build(data, now)).Next?.AtUtc;
        var muted = data.Muted.ToHashSet();

        var entries = BossBoard.Spawns(data, fromUtc, toUtc, followedOnly: false)
            .SelectMany(g => g.Bosses.Select(boss =>
            {
                var state = g.AtUtc <= now ? CellState.Past
                    : !boss.Enabled ? CellState.Unfollowed
                    : muted.Contains(new MutedOccurrence(boss.Id, g.AtUtc)) ? CellState.Skipped
                    : g.AtUtc == next ? CellState.Next
                    : CellState.Upcoming;
                var localTime = TimeZoneInfo.ConvertTime(g.AtUtc, local).DateTime;
                return (Local: localTime, Entry: new GridEntry(boss, g.AtUtc, state));
            }))
            .ToList();

        var rows = entries
            .GroupBy(e => TimeOnly.FromDateTime(e.Local))
            .OrderBy(g => g.Key)
            .Select(row => new GridRow(row.Key, Enumerable.Range(0, 7)
                .Select(day => (IReadOnlyList<GridEntry>)row
                    .Where(e => DateOnly.FromDateTime(e.Local).DayNumber - weekStart.DayNumber == day)
                    .Select(e => e.Entry)
                    .ToList())
                .ToList()))
            .ToList();
        return new WeekGridState(weekStart, rows);
    }
}
