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

    public static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool TryParseMinutes(string text, int min, int max, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
