using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Overlay;

/// <summary>
/// Shows the overlay while it's pinned, summoned by hotkey, due for a pop-up or previewed in the Overlay panel
/// (spec: docs/superpowers/specs/2026-09-28-overlay-design.md). UI thread only.
/// </summary>
public sealed class OverlayController(AppServices services) : IDisposable
{
    OverlayWindow? _window;
    OverlayPresence _presence = new();
    readonly OverlayPopUpGate _popUps = new();
    bool _previewing;

    public HotkeyService Hotkeys { get; } = new();

    public void Start()
    {
        services.UiClock.Tick += Update;
        services.Settings.Changed += OnSettingsChanged;
        services.Timers.Changed += OnTimersChanged;
        Hotkeys.Pressed += OnHotkey;
        HoldHotkeys();
    }

    /// <summary>Shows the overlay as a draggable preview while the Overlay panel is open.</summary>
    public void BeginPreview()
    {
        _previewing = true;
        EnsureWindow().SetClickThrough(false);
        Update(DateTimeOffset.UtcNow);
    }

    public void EndPreview()
    {
        if (!_previewing) return;
        _previewing = false;
        _window?.SetClickThrough(true);
        Update(DateTimeOffset.UtcNow);
    }

    void OnSettingsChanged()
    {
        HoldHotkeys();
        Update(DateTimeOffset.UtcNow);
    }

    void OnTimersChanged() => Application.Current.Dispatcher.BeginInvoke(HoldHotkeys);

    void HoldHotkeys()
    {
        var o = services.Settings.Current.Overlay;
        var horse = services.Timers.Current.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration);
        Hotkeys.Set(o.Enabled ? o.AlwaysShowHotkey : null, o.Enabled && o.ShowOnHotkey ? o.ShowHotkey : null,
            horse?.StartHotkey);
    }

    void OnHotkey(HotkeyAction action)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            if (action == HotkeyAction.StartHorseRegistration)
                services.StartHorseRegistration(announce: true);
            else if (action == HotkeyAction.AlwaysShow)
                services.Settings.Update(s => s with { Overlay = s.Overlay with { AlwaysShow = !s.Overlay.AlwaysShow } });
            else
                _presence = _presence.PressShow(services.Settings.Current.Overlay, now);
        }
        catch (Exception ex)
        {
            Log.Error("Overlay hotkey failed", ex);
        }
        Update(now);
    }

    void Update(DateTimeOffset now)
    {
        try { Refresh(now); }
        catch (Exception ex) { Log.Error("Overlay update failed", ex); }
    }

    void Refresh(DateTimeOffset now)
    {
        var settings = services.Settings.Current.Overlay;
        var data = services.Timers.Current;
        _presence = _presence.Settle(settings);
        // Building the content is most of a tick's work; skip it while nothing can bring the overlay up.
        var content = _presence.MayShow(settings, now, _previewing, _popUps.MayBeDue(data, now))
            ? OverlayContent.Build(data, settings, now, services.Boards)
            : null;
        if (content is null || !_presence.IsVisible(settings, now, content, _previewing))
        {
            _window?.Hide();
            return;
        }
        var window = EnsureWindow();
        window.Model.Update(content, settings, now, _previewing);
        if (!window.IsVisible) window.Show();
        window.KeepOnTop();
    }

    OverlayWindow EnsureWindow()
    {
        if (_window is not null) return _window;
        var s = services.Settings.Current;
        var (left, top) = Placement(s.OverlayLeft, s.OverlayTop);
        _window = new OverlayWindow(new OverlayViewModel(services.Art)) { Left = left, Top = top };
        _window.Dropped += SavePosition;
        return _window;
    }

    /// <summary>The saved spot, unless it's no longer on a screen (a monitor was unplugged, say); then the top right of
    /// the primary screen.</summary>
    static (double Left, double Top) Placement(double? left, double? top)
    {
        return left is { } l && top is { } t && VirtualScreen.Contains(new Point(l + 20, t + 20))
            ? (l, t)
            : (SystemParameters.WorkArea.Right - 300, SystemParameters.WorkArea.Top + 40);
    }

    void SavePosition()
    {
        if (_window is not { } window) return;
        try { services.Settings.Update(s => s with { OverlayLeft = window.Left, OverlayTop = window.Top }); }
        catch (StateSaveException ex) { Log.Error("Couldn't save the overlay position", ex); }
    }

    public void Dispose()
    {
        Hotkeys.Dispose();
        _window?.Close();
    }
}
