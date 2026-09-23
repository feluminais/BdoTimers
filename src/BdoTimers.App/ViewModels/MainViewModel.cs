using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    readonly AppServices _services;

    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";

    public MainViewModel(AppServices services)
    {
        _services = services;
        services.UiClock.Tick += RefreshPaused;
        RefreshPaused(DateTimeOffset.UtcNow);
    }

    void RefreshPaused(DateTimeOffset now)
    {
        var until = _services.Settings.Current.AlertsPausedUntilUtc;
        IsPaused = until is { } u && now < u;
        PausedText = until == DateTimeOffset.MaxValue
            ? "Alerts paused"
            : IsPaused ? $"Alerts paused, {DurationFormat.Countdown(until!.Value - now)} left" : "";
    }

    [RelayCommand]
    void Resume() => _services.ResumeAlerts();
}
