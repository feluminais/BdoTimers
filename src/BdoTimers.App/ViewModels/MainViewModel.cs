using System.Windows;
using BdoTimers.App.Views;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    readonly AppServices _services;

    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";

    public UpcomingViewModel Upcoming { get; }
    public TimersViewModel Timers { get; }

    public MainViewModel(AppServices services)
    {
        _services = services;
        Upcoming = new UpcomingViewModel(services);
        Timers = new TimersViewModel(services);
        services.UiClock.Tick += RefreshPaused;
        RefreshPaused(DateTimeOffset.UtcNow);
    }

    void RefreshPaused(DateTimeOffset now)
    {
        var s = _services.Settings.Current;
        IsPaused = AlertPause.IsPaused(s, now);
        PausedText = !IsPaused ? ""
            : AlertPause.Remaining(s, now) is { } left ? $"Alerts paused, {DurationFormat.Countdown(left)} left"
            : "Alerts paused";
    }

    [RelayCommand]
    void Resume() => _services.ResumeAlerts();

    [RelayCommand]
    void OpenSettings() =>
        new SettingsWindow(new SettingsViewModel(_services))
        {
            Owner = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(),
        }.ShowDialog();
}
