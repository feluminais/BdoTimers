using System.Globalization;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Text;

public sealed record AlertMessage(string Title, string Body, string Speech)
{
    public static AlertMessage Build(AlertEvent alert)
    {
        var names = alert.Timers.Select(t => t.Name).ToList();
        // A shared spawn speaks once, with the phrasing of the first timer that has voice on.
        var tts = (alert.Timers.FirstOrDefault(t => t.Alerts.Tts.Enabled) ?? alert.Timers[0]).Alerts.Tts;
        var isNow = alert.MinutesLeft <= 0;
        var speech = Fill(isNow ? tts.NowTemplate : tts.Template, JoinNames(names), alert.MinutesLeft);
        var local = alert.OccurrenceUtc.ToLocalTime();
        var body = isNow ? $"Now ({local:HH:mm})" : $"In {alert.MinutesLeft} min, at {local:HH:mm}";
        return new AlertMessage(string.Join(" · ", names), body, speech);
    }

    /// <summary>"A", "A and B", "A, B and C".</summary>
    public static string JoinNames(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

    /// <summary>Replaces {name}, {minutes} (a number) and {duration} ("5 minutes") in a template.</summary>
    public static string Fill(string template, string name, int minutes) => template
        .Replace("{name}", name, StringComparison.OrdinalIgnoreCase)
        .Replace("{minutes}", minutes.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        .Replace("{duration}", Humanize(minutes), StringComparison.OrdinalIgnoreCase);

    public static string Humanize(int minutes)
    {
        static string Unit(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";
        if (minutes < 60) return Unit(minutes, "minute");
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? Unit(h, "hour") : $"{Unit(h, "hour")} {Unit(m, "minute")}";
    }
}
