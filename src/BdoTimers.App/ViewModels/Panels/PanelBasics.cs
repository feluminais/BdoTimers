namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Shows one panel at a time over the main window.</summary>
public interface IPanelHost
{
    void OpenPanel(object panel);
    void ClosePanel();
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
