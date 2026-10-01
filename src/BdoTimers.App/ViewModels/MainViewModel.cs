using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IPanelHost
{
    readonly AppServices _services;
    bool _shown;

    [ObservableProperty] private object? _panel;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";

    public BossesViewModel Bosses { get; }
    public CustomViewModel Custom { get; }
    public TodoViewModel Todo { get; }

    public MainViewModel(AppServices services)
    {
        _services = services;
        Bosses = new BossesViewModel(services, this);
        Custom = new CustomViewModel(services, this);
        Todo = new TodoViewModel(services, this);
        RefreshPaused(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The screens follow the clock only while the window can be seen; hidden to the tray or minimized, they skip the
    /// ticks and catch up when it comes back.
    /// </summary>
    public void SetShown(bool shown)
    {
        if (shown == _shown) return;
        _shown = shown;
        if (!shown)
        {
            _services.UiClock.Tick -= Tick;
            return;
        }
        _services.UiClock.Tick += Tick;
        Tick(DateTimeOffset.UtcNow);
    }

    void Tick(DateTimeOffset now)
    {
        Bosses.Refresh(now);
        Custom.Refresh(now);
        Todo.UpdateResetLabels();
        RefreshPaused(now);
    }

    public void OpenPanel(object panel)
    {
        ClosePanel();
        Panel = panel;
    }

    public bool IsOpen(object panel) => ReferenceEquals(Panel, panel);

    [RelayCommand]
    public void ClosePanel()
    {
        (Panel as IPanel)?.OnClosed();
        Panel = null;
    }

    [RelayCommand]
    void OpenSettings() => OpenPanel(new SettingsPanelViewModel(_services));

    /// <summary>Unlike <see cref="OpenPanel"/>, closes the open panel before making the new one: the Overlay panel
    /// starts the overlay preview when it's made, which an open Overlay panel would end on closing.</summary>
    [RelayCommand]
    public void OpenOverlaySettings()
    {
        ClosePanel();
        Panel = new OverlayPanelViewModel(_services);
    }

    [RelayCommand]
    void Resume() => _services.ResumeAlerts();

    void RefreshPaused(DateTimeOffset now)
    {
        var s = _services.Settings.Current;
        IsPaused = AlertPause.IsPaused(s, now);
        PausedText = !IsPaused ? ""
            : AlertPause.Remaining(s, now) is { } left ? $"Alerts paused · {DurationFormat.Countdown(left)}"
            : "Alerts paused";
    }
}
