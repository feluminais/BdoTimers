using System.Windows;

namespace BdoTimers.App.Controls;

/// <summary>Attached state the theme's templates react to.</summary>
public static class Ui
{
    /// <summary>Marks a text field as invalid; the theme draws its hairline in the danger colour.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));

    public static bool GetHasError(DependencyObject d) => (bool)d.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);
}
