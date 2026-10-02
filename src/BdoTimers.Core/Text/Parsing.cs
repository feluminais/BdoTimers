using System.Globalization;

namespace BdoTimers.Core.Text;

public static class Parsing
{
    /// <summary>Invariant "HH:mm"; the current culture may use another separator that <see cref="TryParseTime"/> rejects.</summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>
    /// Puts the colon into a time typed as digits: "2200" → "22:00", "930" → "9:30". Two leading digits make the hour
    /// unless they exceed 23; a lone hour gets no trailing colon, so backspace can still remove it.
    /// </summary>
    public static string AddTimeColon(string text)
    {
        if (text.Length < 2 || !text.All(char.IsAsciiDigit)) return text;
        var hourDigits = (text[0] - '0') * 10 + (text[1] - '0') <= 23 ? 2 : 1;
        return text.Length == hourDigits ? text : $"{text[..hourDigits]}:{text[hourDigits..]}";
    }

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Countdown length as "H:MM" or a plain number of minutes, from 1 minute to 24 hours.</summary>
    public static bool TryParseDuration(string text, out TimeSpan duration)
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
        var totalMinutes = (long)hours * 60 + minutes;
        if (totalMinutes < 1 || totalMinutes > 24 * 60) return false;
        duration = TimeSpan.FromMinutes(totalMinutes);
        return true;
    }

    public static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours}:{duration.Minutes.ToString("00", CultureInfo.InvariantCulture)}";

    public static bool TryParseMinutes(string text, int min, int max, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
