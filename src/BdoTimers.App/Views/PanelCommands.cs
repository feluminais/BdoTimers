using System.Windows.Input;

namespace BdoTimers.App.Views;

/// <summary>
/// What a panel's own buttons ask of the window that shows it. They are routed to the window when pressed, so a view that
/// was built before it was in the window, as the idle ones are, works the same as one built when its panel opens.
/// </summary>
public static class PanelCommands
{
    public static RoutedCommand Close { get; } = new(nameof(Close), typeof(PanelCommands));
    public static RoutedCommand Finish { get; } = new(nameof(Finish), typeof(PanelCommands));
}
