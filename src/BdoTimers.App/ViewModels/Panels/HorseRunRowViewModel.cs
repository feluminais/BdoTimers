using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>One horse registration under way, in the Horse registration panel: its number, its time left, and Stop.</summary>
public sealed partial class HorseRunRowViewModel(Guid id, Action<Guid> stop) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _clock = "";

    [RelayCommand]
    void Stop() => stop(Id);
}
