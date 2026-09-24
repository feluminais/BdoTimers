using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// A timer's sound row in the boss and custom panels: each step is saved; sound plays only from the test button.
/// </summary>
public sealed partial class TimerSoundViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly Guid _id;
    bool _syncing;

    [ObservableProperty] private IReadOnlyList<Choice> _choices = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private Choice? _selected;
    [ObservableProperty] private string? _error;

    public TimerSoundViewModel(AppServices services, TimerDef timer)
    {
        _services = services;
        _id = timer.Id;
        _syncing = true;
        Choices = SoundChoices.ForTimer(services.Sounds);
        Selected = SoundChoices.Matching(Choices, timer.Alerts.Sound);
        _syncing = false;
    }

    SoundAlert? Sound => Selected?.Value as SoundAlert;

    partial void OnSelectedChanged(Choice? value)
    {
        if (_syncing || Sound is not { } sound) return;
        Error = null;
        _services.Timers.Modify(_id, t => t with { Alerts = t.Alerts with { Sound = sound } });
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
}
