using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BdoTimers.App.ViewModels;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Views;

public partial class MainWindow : Window
{
    static readonly Duration Fade = TimeSpan.FromMilliseconds(120);

    readonly MainViewModel _vm;
    readonly AppServices _services;
    readonly PanelFocusScope _panelFocus;
    HwndSource? _source;
    bool _placementPending;

    public MainWindow(MainViewModel viewModel, AppServices services)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        _services = services;
        _panelFocus = new PanelFocusScope(this, MainContent, PanelLayer, _vm.ClosePanel);
        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WindowMessages);
            RestorePlacement(services.Settings.Current.Window);
        };
        Loaded += (_, _) => QueuePlacementCheck();
        DpiChanged += (_, _) => QueuePlacementCheck();
        PreviewKeyDown += UndoKeyDown;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Panel)) ShowPanel(viewModel.Panel);
        };
        StateChanged += (_, _) =>
        {
            // A maximized frameless window overhangs the screen by the resize border.
            Frame.Margin = new Thickness(WindowState == WindowState.Maximized ? 7 : 0);
            var maximized = WindowState == WindowState.Maximized;
            MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
            MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
            AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
            FollowShown();
            if (WindowState == WindowState.Normal) QueuePlacementCheck();
        };
        IsVisibleChanged += (_, _) => { FollowShown(); if (IsVisible) QueuePlacementCheck(); };
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
            _panelFocus.Open();
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
            _panelFocus.Close();
        };
        PanelLayer.BeginAnimation(OpacityProperty, fadeOut);
    }

    void Dim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _vm.ClosePanel();

    /// <summary>The scroller's content presenter takes hits across its whole area, so most of the dim around the panel
    /// is under it; a press that lands on the presenter itself is outside the panel.</summary>
    void PanelScroller_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is ScrollContentPresenter presenter && presenter.TemplatedParent == PanelScroller) _vm.ClosePanel();
    }

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    void ToggleMaximize()
    {
        if (_vm.Panel is not null) return;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    void UndoKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.Panel is not null || e.Key != Key.Z || Keyboard.Modifiers != ModifierKeys.Control) return;
        // Text controls retain their native undo stack, including editable combo boxes.
        if (e.OriginalSource is TextBoxBase || Keyboard.FocusedElement is TextBoxBase) return;
        if (!_vm.Undo.UndoCommand.CanExecute(null)) return;
        _vm.Undo.UndoCommand.Execute(null);
        e.Handled = true;
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void RestorePlacement(WindowPlacement? placement)
    {
        if (placement is { } saved)
        {
            var requested = new WindowRect(saved.Left, saved.Top, saved.Width, saved.Height);
            if (requested.IsValid)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                ApplyPlacement(requested);
                return;
            }
        }
        QueuePlacementCheck();
    }

    void QueuePlacementCheck()
    {
        if (_placementPending) return;
        _placementPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _placementPending = false;
            if (_source is null || WindowState == WindowState.Minimized) return;
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (bounds.IsEmpty || !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top)) return;
            ApplyPlacement(new WindowRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
        });
    }

    void ApplyPlacement(WindowRect requested)
    {
        var areas = VirtualScreen.WorkAreas(this);
        var work = WindowGeometry.WorkAreaFor(requested, areas);
        MinWidth = Math.Min(640, work.Width);
        MinHeight = Math.Min(360, work.Height);
        var clamped = WindowGeometry.Clamp(requested with
        {
            Width = Math.Max(requested.Width, MinWidth), Height = Math.Max(requested.Height, MinHeight)
        }, areas);
        if (WindowState != WindowState.Normal) return;
        Left = clamped.Left;
        Top = clamped.Top;
        Width = clamped.Width;
        Height = clamped.Height;
    }

    IntPtr WindowMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmDisplayChange = 0x007E, WmSettingChange = 0x001A, WmNcHitTest = 0x0084, WmNcLButtonUp = 0x00A2;
        const int HtMaxButton = 9;
        if (message is WmDisplayChange or WmSettingChange) QueuePlacementCheck();
        if (message == WmNcHitTest && MainContent.IsEnabled && MaximizeButton.IsVisible)
        {
            var packed = lParam.ToInt64();
            var point = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff));
            var origin = MaximizeButton.PointToScreen(new Point());
            var end = MaximizeButton.PointToScreen(new Point(MaximizeButton.ActualWidth, MaximizeButton.ActualHeight));
            if (new Rect(origin, end).Contains(point))
            {
                handled = true;
                return new IntPtr(HtMaxButton);
            }
        }
        if (message == WmNcLButtonUp && wParam.ToInt32() == HtMaxButton)
        {
            ToggleMaximize();
            handled = true;
        }
        return IntPtr.Zero;
    }

    void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty) return;
        try
        {
            _services.Settings.Update(s => s with { Window = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height) });
        }
        catch (StateSaveException ex)
        {
            Log.Error("Couldn't save window placement", ex);
            _services.Health.Failed("Saving", ex);
        }
    }

    /// <summary>Hiding to the tray is opt-in; explicit Quit always closes the window.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        _vm.ClosePanel();
        if (!_services.IsQuitting && _services.Settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WindowMessages);
        _source = null;
        base.OnClosed(e);
        if (!_services.IsQuitting) _services.Quit();
    }
}
