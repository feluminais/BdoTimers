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
}
