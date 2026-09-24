using System.Reflection;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class SettingsPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;

    static readonly IReadOnlyDictionary<string, string> SoundLabels = new Dictionary<string, string>
    {
        [BuiltInSounds.Gong] = "Gong",
        [BuiltInSounds.Horn] = "War horn",
        [BuiltInSounds.Bell] = "Low bell",
        [BuiltInSounds.Chime] = "Soft chime",
    };

    [ObservableProperty] private Choice _autostart;
    [ObservableProperty] private Choice _alertSound;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private Choice? _voice;
    [ObservableProperty] private double _speechRate;
    [ObservableProperty] private bool _isPositioningOverlay;
    [ObservableProperty] private bool _confirmingReset;

    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> Voices { get; }
    public IReadOnlyList<Choice> Sounds { get; } = BuiltInSounds.All.Select(k => new Choice(SoundLabels[k], k)).ToList();
    public LeadChipsViewModel DefaultLeads { get; }
    public string Version { get; } = AppVersion();

    public SettingsPanelViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Current;
        _autostart = Choice.For(s.Autostart);
        _volume = s.Volume;
        _alertSound = Sounds.First(c => (string)c.Value! == BuiltInSounds.Resolve(s.AlertSound));
        Voices = services.Tts.InstalledVoices().Select(v => new Choice(v, v)).ToList();
        _voice = Voices.FirstOrDefault(v => (string)v.Value! == s.TtsVoice) ?? Voices.FirstOrDefault();
        _speechRate = s.TtsRate;
        _isPositioningOverlay = services.Overlay.IsPositioning;
        DefaultLeads = new LeadChipsViewModel(s.DefaultLeadTimesMinutes,
            leads => services.Settings.Update(x => x with { DefaultLeadTimesMinutes = leads }));
    }

    partial void OnAutostartChanged(Choice value)
    {
        _services.Settings.Update(s => s with { Autostart = value.IsOn });
        BdoTimers.App.Autostart.Apply(value.IsOn);
    }

    /// <summary>Saves and plays the sound, so cycling through the list auditions each one.</summary>
    partial void OnAlertSoundChanged(Choice value)
    {
        _services.Settings.Update(s => s with { AlertSound = (string)value.Value! });
        _services.PreviewSound();
    }

    partial void OnVolumeChanged(double value) => _services.Settings.Update(s => s with { Volume = (float)value });

    partial void OnVoiceChanged(Choice? value) => _services.Settings.Update(s => s with { TtsVoice = value?.Value as string });

    partial void OnSpeechRateChanged(double value) =>
        _services.Settings.Update(s => s with { TtsRate = (int)Math.Round(value) });

    [RelayCommand]
    void PreviewSound() => _services.PreviewSound();

    [RelayCommand]
    void TestAlert() => _services.SendTestAlert();

    [RelayCommand]
    void OpenNotificationSettings() => ToastChannel.OpenWindowsNotificationSettings();

    [RelayCommand]
    void ToggleOverlayPositioning()
    {
        _services.Overlay.TogglePositioning();
        IsPositioningOverlay = _services.Overlay.IsPositioning;
    }

    [RelayCommand]
    void AskReset() => ConfirmingReset = true;

    [RelayCommand]
    void CancelReset() => ConfirmingReset = false;

    [RelayCommand]
    void ConfirmReset()
    {
        _services.ResetBossTimetable();
        ConfirmingReset = false;
    }

    [RelayCommand]
    void OpenDataFolder() => _services.OpenDataFolder();

    public void OnClosed() => _services.Overlay.FinishPositioning();

    static string AppVersion()
    {
        var info = typeof(SettingsPanelViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info?.Split('+')[0] ?? "";
    }
}
