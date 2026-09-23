using System.Windows;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;

namespace BdoTimers.App.Overlay;

/// <summary>Shows the overlay only while an opted-in event is inside its "show N minutes before" window.</summary>
public sealed class OverlayController(AppServices services)
{
    OverlayWindow? _window;

    public bool IsPositioning { get; private set; }

    public void Start() => services.UiClock.Tick += Update;

    void Update(DateTimeOffset now)
    {
        if (IsPositioning) return;
        var items = UpcomingQuery.ForOverlay(services.Timers.Current, now);
        if (items.Count == 0)
        {
            _window?.Hide();
            return;
        }
        var window = EnsureWindow();
        window.SetRows(items.Select(i => new OverlayRow(i.Timer.Name, DurationFormat.Countdown(i.AtUtc - now))).ToList());
        if (!window.IsVisible) window.Show();
        // Re-assert z-order: a borderless game window can climb above other topmost windows.
        window.Topmost = false;
        window.Topmost = true;
    }

    public void TogglePositioning()
    {
        var window = EnsureWindow();
        if (!IsPositioning)
        {
            IsPositioning = true;
            window.SetRows([new OverlayRow("Drag me into place", "05:00")]);
            window.SetClickThrough(false);
            window.Show();
            return;
        }
        window.SetClickThrough(true);
        services.Settings.Update(s => s with { OverlayLeft = window.Left, OverlayTop = window.Top });
        IsPositioning = false;
        window.Hide();
    }

    OverlayWindow EnsureWindow()
    {
        if (_window is not null) return _window;
        var s = services.Settings.Current;
        _window = new OverlayWindow
        {
            Left = s.OverlayLeft ?? SystemParameters.WorkArea.Right - 300,
            Top = s.OverlayTop ?? 40,
        };
        return _window;
    }
}
