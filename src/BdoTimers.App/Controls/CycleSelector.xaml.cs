using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Input;

namespace BdoTimers.App.Controls;

/// <summary>"‹ value ›" selector: the arrows step through <see cref="ItemsSource"/>, wrapping at both ends.</summary>
public partial class CycleSelector : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IList), typeof(CycleSelector));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(CycleSelector),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedItemChanged));

    public static readonly DependencyProperty ValueWidthProperty =
        DependencyProperty.Register(nameof(ValueWidth), typeof(double), typeof(CycleSelector), new PropertyMetadata(96.0));

    public CycleSelector() => InitializeComponent();

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public double ValueWidth
    {
        get => (double)GetValue(ValueWidthProperty);
        set => SetValue(ValueWidthProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CycleSelectorAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down: Step(-1); break;
            case Key.Right:
            case Key.Up: Step(1); break;
            case Key.Home: SelectIndex(0); break;
            case Key.End: SelectIndex((ItemsSource?.Count ?? 0) - 1); break;
            default: return;
        }
        Ui.UseKeyboardFocus(this);
        e.Handled = true;
    }

    void SelectIndex(int index)
    {
        if (ItemsSource is { } items && index >= 0 && index < items.Count)
            SetCurrentValue(SelectedItemProperty, items[index]);
    }

    static void SelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (UIElementAutomationPeer.FromElement((CycleSelector)d) is CycleSelectorAutomationPeer peer)
            peer.RaiseValueChanged(e.OldValue?.ToString() ?? "", e.NewValue?.ToString() ?? "");
    }

    void Previous_Click(object sender, RoutedEventArgs e) { Focus(); Step(-1); }
    void Next_Click(object sender, RoutedEventArgs e) { Focus(); Step(1); }

    void Step(int delta)
    {
        if (ItemsSource is not { Count: > 0 } items) return;
        var index = SelectedItem is null ? -1 : items.IndexOf(SelectedItem);
        var next = index < 0 ? 0 : (index + delta + items.Count) % items.Count;
        SetCurrentValue(SelectedItemProperty, items[next]);
    }
}
