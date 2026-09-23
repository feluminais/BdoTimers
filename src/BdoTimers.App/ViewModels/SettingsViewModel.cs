using System.Windows;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    readonly AppServices _services;

    public event Action? Closed;

    public IReadOnlyList<string> Voices { get; }

    [ObservableProperty] private bool _autostart;
    [ObservableProperty] private float _volume;
    [ObservableProperty] private string? _ttsVoice;
    [ObservableProperty] private int _ttsRate;
    [ObservableProperty] private string _defaultLeadTimesText;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private bool _isPositioningOverlay;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Current;
        Voices = services.Tts.InstalledVoices();
        _autostart = s.Autostart;
        _volume = s.Volume;
        _ttsVoice = s.TtsVoice ?? Voices.FirstOrDefault();
        _ttsRate = s.TtsRate;
        _defaultLeadTimesText = Parsing.FormatLeadTimes(s.DefaultLeadTimesMinutes);
        _isPositioningOverlay = services.Overlay.IsPositioning;
    }

    /// <summary>Closing the window any way (Save, Cancel, X) ends overlay positioning, which otherwise leaves it stuck on screen and not click-through.</summary>
    public void OnWindowClosed() => _services.Overlay.FinishPositioning();

    [RelayCommand]
    void Save()
    {
        if (!Parsing.TryParseLeadTimes(DefaultLeadTimesText, out var leads, out var error))
        {
            Error = error!;
            return;
        }
        _services.Settings.Update(s => s with
        {
            Autostart = Autostart,
            Volume = Volume,
            TtsVoice = TtsVoice,
            TtsRate = TtsRate,
            DefaultLeadTimesMinutes = leads,
        });
        BdoTimers.App.Autostart.Apply(Autostart);
        Closed?.Invoke();
    }

    [RelayCommand]
    void TestAlert() => _services.SendTestAlert();

    [RelayCommand]
    void OpenNotificationSettings() => ToastChannel.OpenWindowsNotificationSettings();

    [RelayCommand]
    void ResetBosses()
    {
        if (MessageBox.Show("Replace all boss timers with the built-in EU timetable? Your alert settings per boss are kept; custom timers are untouched.",
                "Reset boss timetable", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            _services.ResetBossTimetable();
    }

    [RelayCommand]
    void ToggleOverlayPositioning()
    {
        _services.Overlay.TogglePositioning();
        IsPositioningOverlay = _services.Overlay.IsPositioning;
    }
}
