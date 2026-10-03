using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>The alert rows shared by the boss and custom panels. Every change is saved immediately.</summary>
public sealed partial class AlertRowsViewModel : ObservableObject
{
    /// <summary>The spoken sample uses this many minutes, like a typical early warning.</summary>
    const int SampleMinutes = 5;

    readonly AppServices _services;
    readonly TimerStore _store;
    readonly Guid _id;
    /// <summary>The Guild bosses timer's overlay row shows and changes the Overlay panel's Guild bosses setting.</summary>
    readonly bool _guildBoss;

    [ObservableProperty] private bool _toast;
    [ObservableProperty] private bool _voice;
    [ObservableProperty] private string _voiceLine;
    [ObservableProperty] private bool _voiceLineInvalid;
    [ObservableProperty] private bool _editingVoiceLine;
    [ObservableProperty] private string _voiceSample = "";
    [ObservableProperty] private Choice _overlay;
    [ObservableProperty] private LeadChipsViewModel _leads;
    [ObservableProperty] private bool _leadsFollowDefault;

    public TimerSoundViewModel Sound { get; }
    public IReadOnlyList<Choice> OverlayChoices { get; }
    public string OverlayTip => _guildBoss
        ? "Also in Overlay settings; shows in borderless window mode"
        : "Shows in borderless window mode";

    public AlertRowsViewModel(AppServices services, TimerDef timer)
    {
        _services = services;
        _store = services.Timers;
        _id = timer.Id;
        _guildBoss = timer.Preset == Presets.GuildBosses;
        var a = timer.Alerts;
        // So the voice line's ▶ speaks without first waiting for the model.
        if (a.Tts.Enabled) services.Tts.Warm(services.Settings.Current.TtsVoice);
        Sound = new TimerSoundViewModel(services, timer);
        _toast = a.Toast.Enabled;
        _voice = a.Tts.Enabled;
        _voiceLine = ToEditor(a.Tts.Template);
        UpdateVoiceSample();
        var overlay = _guildBoss ? services.Settings.Current.Overlay.GuildBosses : a.Overlay;
        OverlayChoices = PopUpChoices.For(_guildBoss ? PopUpChoices.GuildBossMinutes : PopUpChoices.TimerMinutes, overlay);
        _overlay = PopUpChoices.Matching(OverlayChoices, overlay);
        _leadsFollowDefault = a.LeadTimesMinutes is null;
        _leads = NewLeads(a.LeadTimes(DefaultLeads));
    }

    IReadOnlyList<int> DefaultLeads => _services.Settings.Current.DefaultLeadTimesMinutes;

    /// <summary>Changing a chip gives the timer its own alert times, so it stops following the default.</summary>
    LeadChipsViewModel NewLeads(IReadOnlyList<int> selected) => new(selected, leads =>
    {
        Modify(a => a with { LeadTimesMinutes = leads });
        LeadsFollowDefault = false;
    });

    [RelayCommand]
    void UseDefaultLeads()
    {
        Modify(a => a with { LeadTimesMinutes = null });
        LeadsFollowDefault = true;
        Leads = NewLeads(DefaultLeads);
    }

    partial void OnToastChanged(bool value) => Modify(a => a with { Toast = a.Toast with { Enabled = value } });

    partial void OnVoiceChanged(bool value) => Modify(a => a with { Tts = a.Tts with { Enabled = value } });

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
    void HearVoiceLine() => _services.Speak(SampleSpeech());

    string SampleSpeech()
    {
        var name = _store.Current.Timers.FirstOrDefault(t => t.Id == _id)?.Name ?? "";
        return AlertMessage.Fill(FromEditor(VoiceLine), name, SampleMinutes);
    }

    void UpdateVoiceSample() => VoiceSample = $"“{SampleSpeech()}”";

    // The editor shows [name] and [time]; saved lines keep the {name} and {duration} placeholders AlertMessage fills.
    static string ToEditor(string template) => template
        .Replace("{name}", "[name]", StringComparison.OrdinalIgnoreCase)
        .Replace("{duration}", "[time]", StringComparison.OrdinalIgnoreCase);

    static string FromEditor(string line) => line
        .Replace("[name]", "{name}", StringComparison.OrdinalIgnoreCase)
        .Replace("[time]", "{duration}", StringComparison.OrdinalIgnoreCase);

    partial void OnOverlayChanged(Choice value)
    {
        if (_guildBoss)
            _services.Settings.Update(s => s with { Overlay = s.Overlay with { GuildBosses = PopUpChoices.Apply(s.Overlay.GuildBosses, value) } });
        else
            Modify(a => a with { Overlay = PopUpChoices.Apply(a.Overlay, value) });
    }

    void Modify(Func<AlertConfig, AlertConfig> change) => _store.Modify(_id, t => t with { Alerts = change(t.Alerts) });
}
