using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Diagnostics;

namespace BdoTimers.Core.Text;

/// <summary>Voice lines for names and alert times, including shared occurrences across the next year.</summary>
public static class SpeechLines
{
    public static IReadOnlyList<string> ForTimers(IEnumerable<TimerDef> timers, IReadOnlyList<int> defaults, IClock clock, Guid? affectedTimer = null)
    {
        var enabled = timers.Where(t => t.Enabled && t.Kind != TimerKind.Stopwatch).ToList();
        var lines = new HashSet<string>();
        foreach (var timer in enabled.Where(t => t.Alerts.Tts.Enabled))
        {
            var labels = timer.Scheduled?.Slots.Select(s => s.Label).Distinct().ToList() ?? [null];
            if (labels.Count == 0) labels.Add(null);
            foreach (var label in labels)
                foreach (var lead in timer.Alerts.LeadTimes(defaults).Where(l => l >= 0).Distinct())
                    Add([(timer, Name(timer, label))], lead);
        }
        var now = clock.UtcNow;
        var upcoming = new List<AlertEvent>();
        foreach (var timer in enabled)
        {
            try
            {
                var alerts = OccurrenceSource.Between(timer, now, now.AddDays(370))
                    .SelectMany(at => timer.Alerts.LeadTimes(defaults).Where(l => l >= 0).Distinct()
                        .Select(lead => new AlertEvent([timer], at, lead, lead))).ToList();
                upcoming.AddRange(alerts);
            }
            catch (Exception ex) { Log.Error($"Couldn't prepare shared voice lines for '{timer.Name}'", ex); }
        }
        foreach (var alert in AlertGrouping.Group(upcoming))
            if (alert.Timers.Any(t => t.Alerts.Tts.Enabled) && (affectedTimer is null || alert.Timers.Any(t => t.Id == affectedTimer)))
                lines.Add(AlertMessage.Build(alert).Speech);
        return lines.Order(StringComparer.Ordinal).ToList();

        void Add(IReadOnlyList<(TimerDef Timer, string Name)> group, int lead)
        {
            if (affectedTimer is { } id && group.All(p => p.Timer.Id != id)) return;
            var sorted = BossOrder.Sort(group.Select(p => p.Timer)).ToList();
            var voice = sorted.FirstOrDefault(t => t.Alerts.Tts.Enabled)?.Alerts.Tts;
            if (voice is null) return;
            var names = sorted.Select(t => group.First(p => p.Timer.Id == t.Id).Name).ToList();
            lines.Add(AlertMessage.Fill(lead == 0 ? voice.NowTemplate : voice.Template, AlertMessage.JoinNames(names), lead));
        }
    }

    static string Name(TimerDef timer, string? label) => string.IsNullOrWhiteSpace(label) ? timer.Name : $"{timer.Name}, {label}";
}
