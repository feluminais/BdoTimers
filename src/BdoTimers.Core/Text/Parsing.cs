using System.Globalization;

namespace BdoTimers.Core.Text;

public static class Parsing
{
    const int MaxLeadMinutes = 1440;

    public static bool TryParseLeadTimes(string text, out IReadOnlyList<int> leads, out string? error)
    {
        leads = [];
        var parts = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "Enter at least one number of minutes, e.g. 15, 5, 1, 0.";
            return false;
        }
        var values = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v > MaxLeadMinutes)
            {
                error = $"\"{part}\" isn't a number of minutes between 0 and {MaxLeadMinutes}.";
                return false;
            }
            values.Add(v);
        }
        leads = values.Distinct().OrderDescending().ToList();
        error = null;
        return true;
    }

    public static string FormatLeadTimes(IEnumerable<int> leads) => string.Join(", ", leads.OrderDescending());

    /// <summary>Invariant "HH:mm"; the current culture may use another separator that <see cref="TryParseTime"/> rejects.</summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Countdown length as "H:MM" or a plain number of minutes, from 1 minute to 24 hours.</summary>
    public static bool TryParseDuration(string text, out TimeSpan duration) =>
        TryParseHoursMinutes(text, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24), out duration);

    /// <summary>"H:MM" or a plain number of minutes, from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static bool TryParseHoursMinutes(string text, TimeSpan min, TimeSpan max, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var parts = text.Trim().Split(':');
        int hours = 0, minutes;
        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out minutes)) return false;
        }
        else if (parts.Length != 2
                 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours)
                 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes)
                 || minutes > 59)
        {
            return false;
        }
        var total = TimeSpan.FromMinutes(hours * 60 + minutes);
        if (total < min || total > max) return false;
        duration = total;
        return true;
    }

    public static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours}:{duration.Minutes.ToString("00", CultureInfo.InvariantCulture)}";

    public static bool TryParseMinutes(string text, int min, int max, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
