using System.Globalization;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Text;

public sealed record AlertMessage(string Title, string Body, string Speech)
{
    public static AlertMessage Build(AlertEvent alert)
    {
        var tts = alert.Timer.Alerts.Tts;
        var isNow = alert.MinutesLeft <= 0;
        var speech = Fill(isNow ? tts.NowTemplate : tts.Template, alert.Timer.Name, alert.MinutesLeft);
        var local = alert.OccurrenceUtc.ToLocalTime();
        var body = isNow ? $"Now ({local:HH:mm})" : $"In {alert.MinutesLeft} min, at {local:HH:mm}";
        return new AlertMessage(alert.Timer.Name, body, speech);
    }

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
