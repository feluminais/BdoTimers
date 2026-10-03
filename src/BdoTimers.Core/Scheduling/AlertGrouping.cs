namespace BdoTimers.Core.Scheduling;

public static class AlertGrouping
{
    /// <summary>
    /// Merges alerts for the same instant and minutes left (bosses sharing a spawn) into one alert whose timers are
    /// sorted with Morning Light bosses first, so a shared spawn plays one sound and speaks one line.
    /// </summary>
    public static IReadOnlyList<AlertEvent> Group(IReadOnlyList<AlertEvent> alerts) =>
        alerts
            .GroupBy(a => (a.OccurrenceUtc, a.MinutesLeft))
            .Select(g => g.Count() == 1
                ? g.First()
                : g.First() with
                {
                    Timers = BossOrder.Sort(g.SelectMany(a => a.Timers)).ToList(),
                })
            .ToList();
}
