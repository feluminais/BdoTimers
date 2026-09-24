using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BdoTimers.SetupUi;

internal partial class MainWindow : Window
{
    readonly SetupViewModel _viewModel;

    public MainWindow(SetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    public IReadOnlyList<ImageSource> WelcomeArt { get; } = [Art("garmoth.jpg")];
    public IReadOnlyList<ImageSource> DoneArt { get; } = [Art("vell.jpg")];

    static ImageSource Art(string file) =>
        new BitmapImage(new Uri($"pack://application:,,,/SetupUi;component/Assets/{file}"));

    /// <summary>The engine parents any prompts of its own to this window.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _viewModel.WindowHandle = new WindowInteropHelper(this).Handle;
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Closing while installing cancels instead; the window closes once the engine has rolled back.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            e.Cancel = true;
            _viewModel.CancelCommand.Execute(null);
        }
        base.OnClosing(e);
    }
}

internal sealed class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the bound <see cref="SetupPage"/> equals the page named in the converter parameter.</summary>
internal sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == (string)parameter ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
