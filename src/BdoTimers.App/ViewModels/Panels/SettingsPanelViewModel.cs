using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class SettingsPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlertSound))]
    private IReadOnlyList<Choice> _alertSounds = [];
    [ObservableProperty] private string? _soundError;
    [ObservableProperty] private bool _hasDeletedTodoDefaults;

    public IReadOnlyList<Choice> Voices { get; } = KokoroEngine.Voices.Select(v => new Choice(v.Label, v.Id)).ToList();
    public ObservableCollection<UserSoundRow> UserSounds { get; } = [];
    public LeadChipsViewModel DefaultLeads { get; }
    public TodoScheduleEditorViewModel DailyTodoReset { get; }
    public TodoScheduleEditorViewModel WeeklyTodoReset { get; }
    public Confirmation AlertReset { get; }
    public Confirmation TimetableReset { get; }
    public string Version { get; } = AppVersion();

    public SettingsPanelViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Current;
        ReloadSounds();
        // So Test voice speaks without first waiting for the model.
        services.Tts.Warm(s.TtsVoice);
        DefaultLeads = new LeadChipsViewModel(s.DefaultLeadTimesMinutes,
            leads => services.Settings.Update(x => x with { DefaultLeadTimesMinutes = leads }));
        DailyTodoReset = new TodoScheduleEditorViewModel(TodoCadence.Daily, s.DailyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Daily, schedule));
        WeeklyTodoReset = new TodoScheduleEditorViewModel(TodoCadence.Weekly, s.WeeklyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Weekly, schedule));
        AlertReset = new Confirmation(services.Timers.ResetBossAlerts);
        TimetableReset = new Confirmation(services.ResetBossTimetable);
        HasDeletedTodoDefaults = services.Todos.Current.Lists.Any(list => list.IsBuiltIn && list.Deleted)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.DailyId)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.WeeklyId);
        services.Settings.Changed += OnSettingsChanged;
    }

    AppSettings Current => _services.Settings.Current;

    public Choice Autostart
    {
        get => Choice.For(Current.Autostart);
        set
        {
            _services.Settings.Update(s => s with { Autostart = value.IsOn });
            BdoTimers.App.Autostart.Apply(value.IsOn);
        }
    }

    public Choice CloseToTray
    {
        get => Choice.For(Current.CloseToTray);
        set => _services.Settings.Update(s => s with { CloseToTray = value.IsOn });
    }

    /// <summary>The saved app-wide sound, or the default when it's gone.</summary>
    public Choice? AlertSound
    {
        get => AlertSounds.FirstOrDefault(c => (string)c.Value! == _services.PlayableSound(null)) ?? AlertSounds[0];
        set
        {
            if (value?.Value is string key) _services.Settings.Update(s => s with { AlertSound = key });
        }
    }

    public double Volume { get => Current.Volume; set => _services.Settings.Update(s => s with { Volume = (float)value }); }

    public Choice? Voice
    {
        get => Voices.FirstOrDefault(v => (string)v.Value! == Current.TtsVoice)
               ?? Voices.FirstOrDefault(v => (string)v.Value! == KokoroEngine.Default.Id)
               ?? Voices.FirstOrDefault();
        set
        {
            _services.Settings.Update(s => s with { TtsVoice = value?.Value as string });
            // A UK voice needs the model loaded for British English, and a US one for American.
            _services.Tts.Warm(value?.Value as string);
        }
    }

    public double SpeechRate
    {
        get => Current.TtsRate;
        set => _services.Settings.Update(s => s with { TtsRate = (int)Math.Round(value) });
    }

    void OnSettingsChanged() => OnPropertyChanged(string.Empty);

    public void OnClosed() => _services.Settings.Changed -= OnSettingsChanged;

    /// <summary>Rebuilds the sound list and the "Your sounds" rows.</summary>
    void ReloadSounds()
    {
        AlertSounds = SoundChoices.ForApp(_services.Sounds);
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
    void RestoreTodoDefaults()
    {
        _services.Todos.RestoreDefaults(_services.Settings.Current);
        HasDeletedTodoDefaults = false;
    }

    [RelayCommand]
    void OpenDataFolder() => _services.OpenDataFolder();

    /// <summary>The GPL asks that the source be offered to everyone who gets the app.</summary>
    [RelayCommand]
    void OpenSource() => Open("https://github.com/feluminais/BdoTimers");

    [RelayCommand]
    void OpenLicenses() => Open(Path.Combine(AppContext.BaseDirectory, "licenses"));

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    static string AppVersion()
    {
        var info = typeof(SettingsPanelViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info?.Split('+')[0] ?? "";
    }
}

/// <summary>A line in Settings' "Your sounds": the sound's name with play and remove buttons.</summary>
public sealed partial class UserSoundRow(string name, string key, Action<string> play, Action<string> remove)
{
    public string Name => name;

    [RelayCommand]
    void Play() => play(key);

    [RelayCommand]
    void Remove() => remove(key);
}
