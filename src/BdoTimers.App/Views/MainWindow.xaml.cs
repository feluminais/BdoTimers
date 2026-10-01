using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using BdoTimers.App.ViewModels;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Views;

public partial class MainWindow : Window
{
    static readonly Duration Fade = TimeSpan.FromMilliseconds(120);

    readonly MainViewModel _vm;
    readonly PersistentState<AppSettings> _settings;

    public MainWindow(MainViewModel viewModel, PersistentState<AppSettings> settings)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        _settings = settings;
        RestorePlacement(settings.Current.Window);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Panel)) ShowPanel(viewModel.Panel);
        };
        StateChanged += (_, _) =>
        {
            // A maximized frameless window overhangs the screen by the resize border.
            Frame.Margin = new Thickness(WindowState == WindowState.Maximized ? 7 : 0);
            FollowShown();
        };
        IsVisibleChanged += (_, _) => FollowShown();
    }

    /// <summary>Hidden to the tray or minimized, the window's screens stop ticking and the process runs with EcoQoS.</summary>
    void FollowShown()
    {
        var shown = IsVisible && WindowState != WindowState.Minimized;
        _vm.SetShown(shown);
        EcoQos.Set(!shown);
    }

    void ShowPanel(object? panel)
    {
        if (panel is not null)
        {
            PanelContent.Content = panel;
            PanelLayer.Visibility = Visibility.Visible;
            PanelLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Fade));
            return;
        }
        // Keep the old content on screen while it fades out.
        var fadeOut = new DoubleAnimation(0, Fade);
        fadeOut.Completed += (_, _) =>
        {
            if (_vm.Panel is not null) return;
            PanelLayer.Visibility = Visibility.Collapsed;
            PanelContent.Content = null;
        };
        PanelLayer.BeginAnimation(OpacityProperty, fadeOut);
    }

    void Dim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _vm.ClosePanel();

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void RestorePlacement(WindowPlacement? placement)
    {
        if (placement is null) return;
        // The top bar must be reachable, or the window couldn't be dragged back from a disconnected screen.
        if (!VirtualScreen.Contains(new Point(placement.Left + 60, placement.Top + 20))) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = Math.Max(placement.Width, MinWidth);
        Height = Math.Max(placement.Height, MinHeight);
    }

    void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty) return;
        try
        {
            _settings.Update(s => s with { Window = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height) });
        }
        catch (StateSaveException ex)
        {
            Log.Error("Couldn't save window placement", ex);
        }
    }

    /// <summary>Hiding to the tray is opt-in; explicit Quit always closes the window.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        _vm.ClosePanel();
        if (!((App)Application.Current).IsQuittingApp && _settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        var app = (App)Application.Current;
        if (!app.IsQuittingApp) app.Quit();
    }
}
