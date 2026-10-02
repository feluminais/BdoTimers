using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

public sealed class CycleSelectorAutomationPeer(CycleSelector owner) : FrameworkElementAutomationPeer(owner), IValueProvider
{
    protected override string GetClassNameCore() => nameof(CycleSelector);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrWhiteSpace(name)) return name;
        for (DependencyObject? parent = VisualTreeHelper.GetParent(owner); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is HeaderedContentControl { Header: { } header }) return header.ToString() ?? "Setting";
        return "Setting";
    }

    protected override string GetItemStatusCore() => Value;
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

    public string Value => owner.SelectedItem?.ToString() ?? "";
    public bool IsReadOnly => !owner.IsEnabled;

    public void SetValue(string value)
    {
        if (!owner.IsEnabled) throw new ElementNotEnabledException();
        if (owner.ItemsSource is { } items)
            foreach (var item in items)
                if (string.Equals(item?.ToString(), value, StringComparison.CurrentCultureIgnoreCase))
                {
                    owner.SetCurrentValue(CycleSelector.SelectedItemProperty, item);
                    return;
                }
        throw new ArgumentException("Choose an available value.", nameof(value));
    }

    internal void RaiseValueChanged(string previous, string value) =>
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, previous, value);
}
