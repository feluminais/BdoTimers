namespace BdoTimers.Core.Text;

public static class DurationFormat
{
    public static string Countdown(TimeSpan left)
    {
        if (left <= TimeSpan.Zero) return "now";
        if (left.TotalDays >= 1) return $"{(int)left.TotalDays}d {left.Hours:00}h";
        if (left.TotalHours >= 1) return $"{(int)left.TotalHours}h {left.Minutes:00}m";
        return $"{left.Minutes:00}:{left.Seconds:00}";
    }

    /// <summary>How long until something: "in 1d 02h", "in 1h 59m", "in 42m", "in 8m 20s" under ten minutes, "now" at zero.</summary>
    public static string Until(TimeSpan left)
    {
        if (left <= TimeSpan.Zero) return "now";
        if (left.TotalDays >= 1) return $"in {(int)left.TotalDays}d {left.Hours:00}h";
        if (left.TotalHours >= 1) return $"in {(int)left.TotalHours}h {left.Minutes:00}m";
        return left.TotalMinutes < 10 ? $"in {left.Minutes}m {left.Seconds:00}s" : $"in {left.Minutes}m";
    }

    /// <summary>"HH:MM:SS", with a day prefix ("1d 02:00:00") from 24 hours up; negative spans read as zero.</summary>
    public static string Clock(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var hms = $"{span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        return span.Days > 0 ? $"{span.Days}d {hms}" : hms;
    }

    /// <summary>A countdown that keeps counting past zero, with a minus sign for overdue time.</summary>
    public static string SignedClock(TimeSpan span) => span < TimeSpan.Zero ? "−" + Clock(-span) : Clock(span);
}
