using System.IO;
using System.Windows;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>The alert rows shared by the boss and custom panels. Every change is saved immediately.</summary>
public sealed partial class AlertRowsViewModel : ObservableObject
{
    static readonly Choice DefaultSound = new("Default", "default");
    static readonly Choice YourFile = new("Your file…", "file");
    static readonly Choice SoundOff = new("Off", "off");
    static readonly int[] OverlayMinutes = [1, 2, 3, 5, 10, 15, 30];

    readonly TimerStore _store;
    readonly Guid _id;

    [ObservableProperty] private Choice _sound;
    [ObservableProperty] private string? _soundFileName;
    [ObservableProperty] private Choice _toast;
    [ObservableProperty] private Choice _voice;
    [ObservableProperty] private string _voicePhrase;
    [ObservableProperty] private bool _voicePhraseInvalid;
    [ObservableProperty] private Choice _overlay;

    public IReadOnlyList<Choice> SoundChoices { get; } = [DefaultSound, YourFile, SoundOff];
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> OverlayChoices { get; }
    public LeadChipsViewModel Leads { get; }

    public AlertRowsViewModel(TimerStore store, TimerDef timer)
    {
        _store = store;
        _id = timer.Id;
        var a = timer.Alerts;
        _sound = !a.Sound.Enabled ? SoundOff : a.Sound.FilePath is null ? DefaultSound : YourFile;
        _soundFileName = a.Sound.FilePath is { } path ? Path.GetFileName(path) : null;
        _toast = Choice.For(a.Toast.Enabled);
        _voice = Choice.For(a.Tts.Enabled);
        _voicePhrase = a.Tts.Template;
        OverlayChoices = [new Choice("Off", 0), .. OverlayMinutes.Union([a.Overlay.ShowMinutesBefore]).Order()
            .Select(m => new Choice($"{m} min before", m))];
        _overlay = a.Overlay.Enabled ? OverlayChoices.First(c => (int)c.Value! == a.Overlay.ShowMinutesBefore) : OverlayChoices[0];
        Leads = new LeadChipsViewModel(a.LeadTimesMinutes, leads => Modify(x => x with { LeadTimesMinutes = leads }));
    }

    partial void OnSoundChanged(Choice? oldValue, Choice newValue)
    {
        if (newValue == YourFile)
        {
            var dialog = new OpenFileDialog { Filter = "Audio files|*.wav;*.mp3|All files|*.*" };
            if (dialog.ShowDialog() != true)
            {
                // Revert after the selector's own update has finished, so it re-reads the old value.
                Application.Current.Dispatcher.BeginInvoke(() => Sound = oldValue ?? DefaultSound);
                return;
            }
            SoundFileName = Path.GetFileName(dialog.FileName);
            Modify(a => a with { Sound = a.Sound with { Enabled = true, FilePath = dialog.FileName } });
            return;
        }
        if (newValue == DefaultSound) SoundFileName = null;
        Modify(a => a with
        {
            Sound = a.Sound with { Enabled = newValue != SoundOff, FilePath = newValue == DefaultSound ? null : a.Sound.FilePath },
        });
    }

    partial void OnToastChanged(Choice value) => Modify(a => a with { Toast = a.Toast with { Enabled = value.IsOn } });

    partial void OnVoiceChanged(Choice value) => Modify(a => a with { Tts = a.Tts with { Enabled = value.IsOn } });

    partial void OnVoicePhraseChanged(string value)
    {
        VoicePhraseInvalid = string.IsNullOrWhiteSpace(value);
        if (!VoicePhraseInvalid) Modify(a => a with { Tts = a.Tts with { Template = value.Trim() } });
    }

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
