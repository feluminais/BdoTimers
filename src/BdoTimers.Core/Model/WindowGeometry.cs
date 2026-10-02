namespace BdoTimers.Core.Model;

/// <summary>A window or monitor work area in one consistent coordinate system.</summary>
public readonly record struct WindowRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public bool IsValid => double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}

public static class WindowGeometry
{
    /// <summary>Choose the monitor with most overlap, or the nearest work area when the saved monitor is gone.</summary>
    public static WindowRect WorkAreaFor(WindowRect window, IReadOnlyList<WindowRect> workAreas)
    {
        if (!window.IsValid) throw new ArgumentException("Invalid window bounds", nameof(window));
        var candidates = workAreas.Where(r => r.IsValid).ToArray();
        if (candidates.Length == 0) throw new ArgumentException("No monitor work areas", nameof(workAreas));
        return candidates.OrderByDescending(r => Overlap(window, r)).ThenBy(r => Distance(window, r)).First();
    }

    public static WindowRect Clamp(WindowRect window, IReadOnlyList<WindowRect> workAreas)
    {
        var work = WorkAreaFor(window, workAreas);
        var width = Math.Min(window.Width, work.Width);
        var height = Math.Min(window.Height, work.Height);
        return new WindowRect(Math.Clamp(window.Left, work.Left, work.Right - width),
            Math.Clamp(window.Top, work.Top, work.Bottom - height), width, height);
    }

    static double Overlap(WindowRect a, WindowRect b) =>
        Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left))
        * Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));

    static double Distance(WindowRect a, WindowRect b)
    {
        var x = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
        var y = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));
        return x * x + y * y;
    }
}
