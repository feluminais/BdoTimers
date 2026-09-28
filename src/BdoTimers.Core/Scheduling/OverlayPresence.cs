using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

/// <summary>
/// Whether the overlay shows. <see cref="ShowUntilUtc"/> is the end of a show-on-hotkey press; the rest comes from the
/// settings, the content and whether the Overlay panel is previewing it.
/// </summary>
public sealed record OverlayPresence(DateTimeOffset? ShowUntilUtc = null)
{
    public bool IsShowing(DateTimeOffset now) => ShowUntilUtc > now;

    /// <summary>The show-on-hotkey press: starts a timed show, or ends one that is running. Ignored while the overlay
    /// or the feature is off, or Always show already keeps it up.</summary>
    public OverlayPresence PressShow(OverlaySettings settings, DateTimeOffset now)
    {
        if (!AllowsTimedShow(settings)) return this;
        return IsShowing(now) ? new OverlayPresence() : new OverlayPresence(now.AddSeconds(settings.ShowSeconds));
    }

    /// <summary>Drops a timed show the settings no longer allow, so it can't return when Always show is turned off.</summary>
    public OverlayPresence Settle(OverlaySettings settings) =>
        ShowUntilUtc is not null && !AllowsTimedShow(settings) ? new OverlayPresence() : this;

    public bool IsVisible(OverlaySettings settings, DateTimeOffset now, OverlaySnapshot content, bool previewing) =>
        settings.Enabled
        && (previewing || (!content.IsEmpty && (settings.AlwaysShow || IsShowing(now) || content.HasDuePopUp)));

    static bool AllowsTimedShow(OverlaySettings s) => s.Enabled && s.ShowOnHotkey && !s.AlwaysShow;
}
