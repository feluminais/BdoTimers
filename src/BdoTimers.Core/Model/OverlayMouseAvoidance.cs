namespace BdoTimers.Core.Model;

/// <summary>Pointer proximity with a wider exit margin so the overlay stays steady at its edge.</summary>
public readonly record struct OverlayMouseAvoidance(bool IsNear = false)
{
    public OverlayMouseAvoidance Update(OverlayMouseProximity mode, WindowRect bounds, double x, double y)
    {
        if (mode is not (OverlayMouseProximity.Fade or OverlayMouseProximity.Hide)
            || !bounds.IsValid || !double.IsFinite(x) || !double.IsFinite(y)) return new();
        var margin = IsNear ? 40 : 24;
        var dx = Math.Max(0, Math.Max(bounds.Left - x, x - bounds.Right));
        var dy = Math.Max(0, Math.Max(bounds.Top - y, y - bounds.Bottom));
        return new(dx * dx + dy * dy <= margin * margin);
    }

    public double Opacity(OverlayMouseProximity mode) => !IsNear ? 1 : mode switch
    {
        OverlayMouseProximity.Fade => .15,
        OverlayMouseProximity.Hide => 0,
        _ => 1,
    };
}
