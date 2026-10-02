using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BdoTimers.App.Controls;

/// <summary>True collapses, false shows.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Null collapses, anything else shows.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>A size, but never less than <c>parameter</c> pixels.</summary>
public sealed class AtLeastConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Math.Max(value is double size ? size : 0, double.Parse((string)parameter, CultureInfo.InvariantCulture));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>How many columns of at least <c>parameter</c> pixels fit in a width; never fewer than two.</summary>
public sealed class WidthToColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Math.Max(2, (int)((value is double width ? width : 0) / double.Parse((string)parameter, CultureInfo.InvariantCulture)));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
