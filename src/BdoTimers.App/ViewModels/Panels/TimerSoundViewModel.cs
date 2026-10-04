using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// A timer's draft sound row; sound plays only from the test button.
/// </summary>
public sealed partial class TimerSoundViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly Guid _id;
    readonly TimerEditor _editor;
    bool _syncing;

    [ObservableProperty] private IReadOnlyList<Choice> _choices = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    [NotifyPropertyChangedFor(nameof(IsUserSound))]
    private Choice? _selected;
    [ObservableProperty] private string? _error;

    public TimerSoundViewModel(AppServices services, TimerDef timer, TimerEditor editor)
    {
        _services = services;
        _id = timer.Id;
        _editor = editor;
        _choices = SoundChoices.ForTimer(services.Sounds);
        _selected = SoundChoices.Matching(_choices, timer.Alerts.Sound);
    }

    SoundAlert? Sound => Selected?.Value as SoundAlert;

    /// <summary>A sound the user added is selected; only those can be removed.</summary>
    public bool IsUserSound => Sound is { Key: { } key } && SoundKeys.IsUserKey(key);

    partial void OnSelectedChanged(Choice? value)
    {
        if (_syncing || Sound is not { } sound) return;
        Error = null;
        _editor.Modify(t => t with { Alerts = t.Alerts with { Sound = sound } });
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    void Test() => _services.PlaySound(Sound!.Key);

    bool CanTest() => Sound is { Enabled: true };

    /// <summary>Adds a user sound and selects it for this timer.</summary>
    [RelayCommand]
    void Add()
    {
        var (key, error) = _services.AddSound();
        Error = error;
        if (key is null) return;
        Choices = SoundChoices.ForTimer(_services.Sounds);
        Selected = SoundChoices.Matching(Choices, new SoundAlert { Key = key });
    }

    /// <summary>Deletes the selected user sound; this timer and any other that used it go back to Default.</summary>
    [RelayCommand]
    void Remove()
    {
        if (!IsUserSound) return;
        Error = _services.RemoveSound(Sound!.Key!);
        if (Error is not null) return;
        _syncing = true;
        Choices = SoundChoices.ForTimer(_services.Sounds);
        Selected = SoundChoices.Matching(Choices, _services.Timers.Current.Timers.First(t => t.Id == _id).Alerts.Sound);
        _syncing = false;
        if (Selected?.Value is SoundAlert replacement) _editor.Modify(t => t with { Alerts = t.Alerts with { Sound = replacement } });
    }
}
