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

    /// <summary>"HH:MM:SS", with a day prefix ("1d 02:00:00") from 24 hours up; negative spans read as zero.</summary>
    public static string Clock(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var hms = $"{span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        return span.Days > 0 ? $"{span.Days}d {hms}" : hms;
    }
}
