using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public enum Section { Bosses, Custom }

public sealed partial class MainViewModel : ObservableObject, IPanelHost
{
    readonly AppServices _services;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBossesSection), nameof(IsCustomSection))]
    private Section _section;

    [ObservableProperty] private object? _panel;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";

    public BossesViewModel Bosses { get; }
    public CustomViewModel Custom { get; }

    public bool IsBossesSection
    {
        get => Section == Section.Bosses;
        set { if (value) Section = Section.Bosses; }
    }

    public bool IsCustomSection
    {
        get => Section == Section.Custom;
        set { if (value) Section = Section.Custom; }
    }

    public MainViewModel(AppServices services)
    {
        _services = services;
        Bosses = new BossesViewModel(services, this);
        Custom = new CustomViewModel(services, this);
        services.UiClock.Tick += RefreshPaused;
        RefreshPaused(DateTimeOffset.UtcNow);
    }

    public void OpenPanel(object panel)
    {
        ClosePanel();
        Panel = panel;
    }

    [RelayCommand]
    public void ClosePanel()
    {
        (Panel as IPanel)?.OnClosed();
        Panel = null;
    }

    [RelayCommand]
    void OpenSettings() => OpenPanel(new SettingsPanelViewModel(_services));

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
