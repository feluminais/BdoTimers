using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class SettingsPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    bool _syncingSound;

    [ObservableProperty] private Choice _autostart;
    [ObservableProperty] private IReadOnlyList<Choice> _alertSounds = [];
    [ObservableProperty] private Choice? _alertSound;
    [ObservableProperty] private string? _soundError;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private Choice? _voice;
    [ObservableProperty] private double _speechRate;
    [ObservableProperty] private bool _isPositioningOverlay;
    [ObservableProperty] private bool _confirmingReset;
    [ObservableProperty] private bool _confirmingAlertReset;

    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> Voices { get; }
    public ObservableCollection<UserSoundRow> UserSounds { get; } = [];
    public LeadChipsViewModel DefaultLeads { get; }
    public string Version { get; } = AppVersion();

    public SettingsPanelViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Current;
        _autostart = Choice.For(s.Autostart);
        _volume = s.Volume;
        ReloadSounds();
        Voices = services.Tts.Voices().Select(v => new Choice(v.Label, v.Id)).ToList();
        _voice = Voices.FirstOrDefault(v => (string)v.Value! == s.TtsVoice)
                 ?? Voices.FirstOrDefault(v => (string)v.Value! == services.Tts.DefaultVoiceId)
                 ?? Voices.FirstOrDefault();
        // So Test voice speaks without first waiting for the model.
        services.Tts.Warm(s.TtsVoice);
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

    partial void OnAlertSoundChanged(Choice? value)
    {
        if (!_syncingSound && value?.Value is string key) _services.Settings.Update(s => s with { AlertSound = key });
    }

    /// <summary>Rebuilds the sound list and the "Your sounds" rows; shows the saved app-wide sound.</summary>
    void ReloadSounds()
    {
        _syncingSound = true;
        AlertSounds = SoundChoices.ForApp(_services.Sounds);
        var saved = _services.PlayableSound(null);
        AlertSound = AlertSounds.FirstOrDefault(c => (string)c.Value! == saved) ?? AlertSounds[0];
        _syncingSound = false;
        UserSounds.Clear();
        foreach (var key in _services.Sounds.Keys())
            UserSounds.Add(new UserSoundRow(SoundChoices.Label(key), key, _services.PlaySound, RemoveSound));
    }

    [RelayCommand]
    void AddSound()
    {
        var (key, error) = _services.AddSound();
        SoundError = error;
        if (key is not null) ReloadSounds();
    }

    void RemoveSound(string key)
    {
        SoundError = _services.RemoveSound(key);
        ReloadSounds();
    }

    partial void OnVolumeChanged(double value) => _services.Settings.Update(s => s with { Volume = (float)value });

    partial void OnVoiceChanged(Choice? value)
    {
        _services.Settings.Update(s => s with { TtsVoice = value?.Value as string });
        // A UK voice needs the model loaded for British English, and a US one for American.
        _services.Tts.Warm(value?.Value as string);
    }

    partial void OnSpeechRateChanged(double value) =>
        _services.Settings.Update(s => s with { TtsRate = (int)Math.Round(value) });

    [RelayCommand]
    void PreviewSound() => _services.PlaySound(null);

    [RelayCommand]
    void TestAlert() => _services.SendTestAlert();

    /// <summary>Two respelled boss names, so the test also shows pronunciation.</summary>
    [RelayCommand]
    void TestVoice() => _services.Speak("Kzarka and Uturi in 5 minutes");


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
    void AskAlertReset() => ConfirmingAlertReset = true;

    [RelayCommand]
    void CancelAlertReset() => ConfirmingAlertReset = false;

    [RelayCommand]
    void ConfirmAlertReset()
    {
        _services.Timers.ResetBossAlerts();
        ConfirmingAlertReset = false;
    }

    [RelayCommand]
    void OpenDataFolder() => _services.OpenDataFolder();

    /// <summary>The GPL asks that the source be offered to everyone who gets the app.</summary>
    [RelayCommand]
    void OpenSource() => Open("https://github.com/feluminais/BdoTimers");

    [RelayCommand]
    void OpenLicenses() => Open(Path.Combine(AppContext.BaseDirectory, "licenses"));

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    public void OnClosed() => _services.Overlay.FinishPositioning();

    static string AppVersion()
    {
        var info = typeof(SettingsPanelViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info?.Split('+')[0] ?? "";
    }
}

/// <summary>A line in Settings' "Your sounds": the sound's name with play and remove buttons.</summary>
public sealed class UserSoundRow(string name, string key, Action<string> play, Action<string> remove)
{
    public string Name => name;
    public IRelayCommand PlayCommand { get; } = new RelayCommand(() => play(key));
    public IRelayCommand RemoveCommand { get; } = new RelayCommand(() => remove(key));
}
