using System.Globalization;

namespace BdoTimers.Core.Text;

public static class Parsing
{
    /// <summary>Invariant "HH:mm"; the current culture may use another separator that <see cref="TryParseTime"/> rejects.</summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Countdown length as "H:MM" or a plain number of minutes, from 1 minute to 24 hours.</summary>
    public static bool TryParseDuration(string text, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (!TryDurationMinutes(text.Trim().Split(':'), out var totalMinutes)
            || totalMinutes < 1 || totalMinutes > 24 * 60) return false;
        duration = TimeSpan.FromMinutes(totalMinutes);
        return true;
    }

    static bool TryDurationMinutes(string[] parts, out long totalMinutes)
    {
        totalMinutes = 0;
        if (parts.Length == 1)
        {
            if (!TryDigits(parts[0], out var only)) return false;
            totalMinutes = only;
            return true;
        }
        if (parts.Length != 2 || !TryDigits(parts[0], out var hours) || !TryDigits(parts[1], out var minutes) || minutes > 59)
            return false;
        totalMinutes = (long)hours * 60 + minutes;
        return true;
    }

    static bool TryDigits(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    public static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours}:{duration.Minutes.ToString("00", CultureInfo.InvariantCulture)}";

    public static bool TryParseMinutes(string text, int min, int max, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
