using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Shows one panel at a time over the main window.</summary>
public interface IPanelHost
{
    void OpenPanel(object panel);
    void ClosePanel();
    bool IsOpen(object panel);
    void CompletePanelEdits() { }
    /// <summary>Closes the open panel for the Overlay panel.</summary>
    void OpenOverlaySettings() { }
}

/// <summary>How a panel shows: a drawer from the right edge, or a centred sheet.</summary>
public enum PanelPresentation { Drawer, Sheet }

/// <summary>Optional draft validation and cleanup for a modal panel.</summary>
public interface IPanel
{
    bool CanFinish => true;
    PanelPresentation Presentation => PanelPresentation.Drawer;
    void OnClosed() { }
}

public interface IDraftPanel : IPanel
{
    bool HasChanges { get; }
    bool IsBusy => false;
    Task SaveAsync();
}

/// <summary>An option in a <see cref="Controls.CycleSelector"/>; records compare by value so fresh instances still match.</summary>
public sealed record Choice(string Label, object? Value)
{
    public static IReadOnlyList<Choice> Days { get; } =
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new Choice(d.ToString(), d)).ToList();

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
