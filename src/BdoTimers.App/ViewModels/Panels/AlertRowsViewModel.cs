using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>The alert rows shared by the boss and custom panels. Every change is saved immediately.</summary>
public sealed partial class AlertRowsViewModel : ObservableObject
{
    static readonly int[] OverlayMinutes = [1, 2, 3, 5, 10, 15, 30];

    /// <summary>The spoken sample uses this many minutes, like a typical early warning.</summary>
    const int SampleMinutes = 5;

    readonly AppServices _services;
    readonly TimerStore _store;
    readonly Guid _id;

    [ObservableProperty] private Choice _toast;
    [ObservableProperty] private Choice _voice;
    [ObservableProperty] private string _voiceLine;
    [ObservableProperty] private bool _voiceLineInvalid;
    [ObservableProperty] private bool _editingVoiceLine;
    [ObservableProperty] private string _voiceSample = "";
    [ObservableProperty] private Choice _overlay;

    public TimerSoundViewModel Sound { get; }
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> OverlayChoices { get; }
    public LeadChipsViewModel Leads { get; }

    public AlertRowsViewModel(AppServices services, TimerDef timer)
    {
        _services = services;
        _store = services.Timers;
        _id = timer.Id;
        var a = timer.Alerts;
        Sound = new TimerSoundViewModel(services, timer);
        _toast = Choice.For(a.Toast.Enabled);
        _voice = Choice.For(a.Tts.Enabled);
        _voiceLine = ToEditor(a.Tts.Template);
        UpdateVoiceSample();
        OverlayChoices = [new Choice("Off", 0), .. OverlayMinutes.Union([a.Overlay.ShowMinutesBefore]).Order()
            .Select(m => new Choice($"{m} min before", m))];
        _overlay = a.Overlay.Enabled ? OverlayChoices.First(c => (int)c.Value! == a.Overlay.ShowMinutesBefore) : OverlayChoices[0];
        Leads = new LeadChipsViewModel(a.LeadTimesMinutes, leads => Modify(x => x with { LeadTimesMinutes = leads }));
    }

    partial void OnToastChanged(Choice value) => Modify(a => a with { Toast = a.Toast with { Enabled = value.IsOn } });

    partial void OnVoiceChanged(Choice value) => Modify(a => a with { Tts = a.Tts with { Enabled = value.IsOn } });

    partial void OnVoiceLineChanged(string value)
    {
        VoiceLineInvalid = string.IsNullOrWhiteSpace(value);
        if (VoiceLineInvalid) return;
        Modify(a => a with { Tts = a.Tts with { Template = FromEditor(value.Trim()) } });
        UpdateVoiceSample();
    }

    [RelayCommand]
    void EditVoiceLine() => EditingVoiceLine = !EditingVoiceLine;

    [RelayCommand]
    void ResetVoiceLine() => VoiceLine = ToEditor(new TtsAlert().Template);

    [RelayCommand]
    async Task HearVoiceLine()
    {
        var s = _services.Settings.Current;
        try { await _services.Tts.SpeakAsync(SampleSpeech(), s.TtsVoice, s.TtsRate, s.Volume); }
        catch (Exception ex) { Log.Error("Voice preview failed", ex); }
    }

    string SampleSpeech()
    {
        var name = _store.Current.Timers.FirstOrDefault(t => t.Id == _id)?.Name ?? "";
        return AlertMessage.Fill(FromEditor(VoiceLine), name, SampleMinutes);
    }

    void UpdateVoiceSample() => VoiceSample = $"Says “{SampleSpeech()}”";

    // The editor shows [name] and [time]; saved lines keep the {name} and {duration} placeholders AlertMessage fills.
    static string ToEditor(string template) => template
        .Replace("{name}", "[name]", StringComparison.OrdinalIgnoreCase)
        .Replace("{duration}", "[time]", StringComparison.OrdinalIgnoreCase);

    static string FromEditor(string line) => line
        .Replace("[name]", "{name}", StringComparison.OrdinalIgnoreCase)
        .Replace("[time]", "{duration}", StringComparison.OrdinalIgnoreCase);

    partial void OnOverlayChanged(Choice value)
    {
        var minutes = (int)value.Value!;
        Modify(a => a with
        {
            Overlay = minutes == 0
                ? a.Overlay with { Enabled = false }
                : a.Overlay with { Enabled = true, ShowMinutesBefore = minutes },
        });
    }

    void Modify(Func<AlertConfig, AlertConfig> change) => _store.Modify(_id, t => t with { Alerts = change(t.Alerts) });
}
