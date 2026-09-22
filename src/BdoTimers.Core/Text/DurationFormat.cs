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
}
