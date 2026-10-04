using System.Globalization;

namespace BdoTimers.Core.Text;

/// <summary>Helps typing times into text boxes. Also built into the setup window, so it stays within .NET Framework.</summary>
public static class TimeEntry
{
    /// <summary>
    /// Puts the colon into a time typed as digits: "2200" → "22:00", "930" → "9:30". Two leading digits make the hour
    /// unless they exceed 23; a lone hour gets no trailing colon, so backspace can still remove it. A space or dot typed
    /// after the hour becomes the colon, with the hour in two digits: "9 " → "09:".
    /// </summary>
    public static string AddColon(string text)
    {
        var beforeSeparator = text.Length is 2 or 3 && text[text.Length - 1] is ' ' or '.' ? text.Substring(0, text.Length - 1) : "";
        if (Digits(beforeSeparator, 1, 2))
        {
            var hour = int.Parse(beforeSeparator, CultureInfo.InvariantCulture);
            return hour <= 23 ? hour.ToString("00", CultureInfo.InvariantCulture) + ":" : text;
        }
        if (text.Length < 2 || !text.All(c => c is >= '0' and <= '9')) return text;
        var hourDigits = (text[0] - '0') * 10 + (text[1] - '0') <= 23 ? 2 : 1;
        return text.Length == hourDigits ? text : $"{text.Substring(0, hourDigits)}:{text.Substring(hourDigits)}";
    }

    /// <summary>
    /// Completes a time when typing is done: a space or dot also separates hour and minutes, a lone hour gets ":00" and
    /// a single minute digit is the tens, so "9" → "09:00", "134" and "13.4" → "13:40", and "1 2" → "01:20". Text
    /// that still isn't a time comes back unchanged.
    /// </summary>
    public static string Finish(string text)
    {
        var parts = AddColon(text.Trim().Replace(' ', ':').Replace('.', ':')).Split(':');
        if (parts.Length > 2 || !Digits(parts[0], 1, 2) || parts.Length == 2 && !Digits(parts[1], 0, 2)) return text;
        var hour = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minute = parts.Length == 1 || parts[1].Length == 0
            ? 0 : int.Parse(parts[1].PadRight(2, '0'), CultureInfo.InvariantCulture);
        return hour <= 23 && minute <= 59
            ? hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture)
            : text;
    }

    static bool Digits(string text, int min, int max) =>
        text.Length >= min && text.Length <= max && text.All(c => c is >= '0' and <= '9');
}
