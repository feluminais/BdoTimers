namespace BdoTimers.Core.Text;

/// <summary>Helps typing times into text boxes. Also built into the setup window, so it stays within .NET Framework.</summary>
public static class TimeEntry
{
    /// <summary>
    /// Puts the colon into a time typed as digits: "2200" → "22:00", "930" → "9:30". Two leading digits make the hour
    /// unless they exceed 23; a lone hour gets no trailing colon, so backspace can still remove it.
    /// </summary>
    public static string AddColon(string text)
    {
        if (text.Length < 2 || !text.All(c => c is >= '0' and <= '9')) return text;
        var hourDigits = (text[0] - '0') * 10 + (text[1] - '0') <= 23 ? 2 : 1;
        return text.Length == hourDigits ? text : $"{text.Substring(0, hourDigits)}:{text.Substring(hourDigits)}";
    }
}
