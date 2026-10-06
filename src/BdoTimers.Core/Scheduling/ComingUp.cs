using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>What the Today screen lists: the next 24 hours of the calendar.</summary>
public static class ComingUp
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>
    /// The calendar's items in [<paramref name="now"/>, now + 24 h). Bosses whose alerts are off are left out; only the
    /// schedule shows them. A skipped spawn stays, marked skipped.
    /// </summary>
    public static IReadOnlyList<CalendarItem> Between(AppData data, AppSettings settings, DateTimeOffset now,
        TimeZoneInfo? local = null, BossBoardCache? boards = null) =>
        CalendarQuery.Between(data, settings, now, now + Window, now, local, boards)
            .Where(i => i is not { Kind: CalendarKind.Boss, State: CellState.Unfollowed })
            .ToList();

    /// <summary>
    /// The items as rows: bosses that spawn at the same time share one, and everything else has its own. The items are in
    /// time order, so the bosses of one spawn are next to each other.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<CalendarItem>> Rows(IEnumerable<CalendarItem> items)
    {
        var rows = new List<List<CalendarItem>>();
        foreach (var item in items)
        {
            if (item.Kind == CalendarKind.Boss && rows.Count > 0 && rows[^1][0] is { Kind: CalendarKind.Boss } last && last.AtUtc == item.AtUtc)
                rows[^1].Add(item);
            else
                rows.Add([item]);
        }
        return rows.Select(row => (IReadOnlyList<CalendarItem>)row).ToList();
    }
}
