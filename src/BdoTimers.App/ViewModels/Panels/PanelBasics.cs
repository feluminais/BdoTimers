using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Shows one panel at a time over the main window.</summary>
public interface IPanelHost
{
    void OpenPanel(object panel);
    void ClosePanel();
    bool IsOpen(object panel);
}

/// <summary>Implemented by panels that need to tidy up when closed by Done, Esc or a click outside.</summary>
public interface IPanel
{
    void OnClosed();
}

/// <summary>An option in a <see cref="Controls.CycleSelector"/>; records compare by value so fresh instances still match.</summary>
public sealed record Choice(string Label, object? Value)
{
    public static readonly IReadOnlyList<Choice> OnOff = [new("On", true), new("Off", false)];

    public static Choice For(bool on) => on ? OnOff[0] : OnOff[1];

    public static IReadOnlyList<Choice> Days { get; } =
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new Choice(d.ToString(), d)).ToList();

    public bool IsOn => Value is true;

    public override string ToString() => Label;
}

/// <summary>An action that asks first: Ask shows the question, Confirm runs the action and Cancel drops it.</summary>
/// <param name="hideAfter">False for an action that closes the panel, so the question stays as the panel fades out.</param>
public sealed partial class Confirmation(Action action, bool hideAfter = true) : ObservableObject
{
    [ObservableProperty] private bool _isAsking;

    [RelayCommand]
    void Ask() => IsAsking = true;

    [RelayCommand]
    void Cancel() => IsAsking = false;

    [RelayCommand]
    void Confirm()
    {
        action();
        if (hideAfter) IsAsking = false;
    }
}
