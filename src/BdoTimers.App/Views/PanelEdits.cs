using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace BdoTimers.App.Views;

/// <summary>Finish delayed field updates before the panel saves or releases its resources.</summary>
internal static class PanelEdits
{
    public static void Complete(DependencyObject panel)
    {
        // Collect first: a source update can change the visible layout.
        var bindings = PanelFocusScope.Descendants(panel).Prepend(panel).Select(element => element switch
        {
            TextBox box => box.GetBindingExpression(TextBox.TextProperty),
            Slider slider => slider.GetBindingExpression(Slider.ValueProperty),
            _ => null,
        }).OfType<BindingExpression>().Where(binding => binding.IsDirty).ToArray();
        foreach (var binding in bindings) binding.UpdateSource();
    }
}
