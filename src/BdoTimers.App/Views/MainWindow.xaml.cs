using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Views;

public partial class MainWindow : Window
{
    static readonly Duration Quick = TimeSpan.FromMilliseconds(167), Slide = TimeSpan.FromMilliseconds(250);
    // Windows' motion curves: fast out and slow in for what enters, the reverse for what leaves.
    static readonly KeySpline Entering = new(0, 0, 0, 1), Leaving = new(1, 0, 1, 1);
    const double BaseMinWidth = 640, BaseMinHeight = 360, BaseCaptionHeight = 44;

    static readonly DependencyProperty UiScaleProperty = DependencyProperty.Register(nameof(UiScale), typeof(ScaleTransform),
        typeof(MainWindow), new PropertyMetadata(null, (d, e) => ((MainWindow)d).UiScaleChanged((ScaleTransform?)e.OldValue)));

    readonly MainViewModel _vm;
    readonly AppServices _services;
    readonly PanelFocusScope _panelFocus;
    HwndSource? _source;
    bool _placementPending;
    // The size the last text size change asked for, which the screen may have held back; while the window keeps the size
    // it got, the next change scales from this, so returning to a smaller text size restores the earlier window.
    Size? _scaledSize;
    Size _appliedSize;

    public MainWindow(MainViewModel viewModel, AppServices services)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        _services = services;
        _panelFocus = new PanelFocusScope(this, MainContent, PanelLayer, _vm.ClosePanel);
        viewModel.CompletingPanelEdits += () => PanelEdits.Complete(PanelContent);
        // Button commands (including previews) use the current draft, even inside a binding's debounce interval.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (_vm.Panel is not null && e.OriginalSource is ButtonBase { Command: not null } button
                && PanelContent.IsAncestorOf(button)) PanelEdits.Complete(PanelContent);
        }));
        SetResourceReference(UiScaleProperty, "UiScaleTransform");
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
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
            {
                TabOf(viewModel.SelectedTab).IsChecked = true;
                UpdateChip();
            }
            if (e.PropertyName == nameof(MainViewModel.AskingDiscard) && viewModel.AskingDiscard)
                Dispatcher.BeginInvoke(() => PanelFocusScope.Descendants(PanelLayer).OfType<Button>()
                    .FirstOrDefault(b => Equals(b.Content, "Keep editing"))?.Focus());
        };
        viewModel.Today.Hero.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HeroViewModel.HasNext)) UpdateChip();
        };
        SizeChanged += (_, _) => UpdateChip();
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

    static DoubleAnimationUsingKeyFrames Move(double from, double to, Duration length, KeySpline spline) => new()
    {
        KeyFrames =
        {
            new DiscreteDoubleKeyFrame(from, KeyTime.FromPercent(0)),
            new SplineDoubleKeyFrame(to, KeyTime.FromPercent(1), spline),
        },
        Duration = length,
    };

    void ShowPanel(object? panel)
    {
        var animate = SystemParameters.ClientAreaAnimation;
        var sheet = _vm.PanelPresentation == PanelPresentation.Sheet;
        if (panel is not null)
        {
            var wasVisible = PanelLayer.Visibility == Visibility.Visible;
            PanelContent.Content = panel;
            PanelContent.ClearValue(IsEnabledProperty);
            PanelLayer.Visibility = Visibility.Visible;
            PanelLayer.UpdateLayout();
            ApplyPanelBounds();
            _panelFocus.Open();
            // A panel that replaces another stays where it is; only a layer that was closed slides or fades in.
            FrameShift.BeginAnimation(TranslateTransform.XProperty, null);
            PanelFrame.BeginAnimation(OpacityProperty, null);
            Scrim.BeginAnimation(OpacityProperty, null);
            FrameShift.X = 0;
            PanelFrame.Opacity = 1;
            Scrim.Opacity = 1;
            if (wasVisible || !animate) return;
            Scrim.BeginAnimation(OpacityProperty, Move(0, 1, Quick, Entering));
            if (sheet) PanelFrame.BeginAnimation(OpacityProperty, Move(0, 1, Quick, Entering));
            else FrameShift.BeginAnimation(TranslateTransform.XProperty, Move(PanelFrame.ActualWidth * UiScale, 0, Slide, Entering));
            return;
        }
        // Keep the old content on screen while it leaves.
        PanelContent.IsEnabled = false;
        void Finished()
        {
            if (_vm.Panel is not null) return;
            PanelLayer.Visibility = Visibility.Collapsed;
            PanelContent.Content = null;
            _panelFocus.Close();
        }
        if (!animate)
        {
            Scrim.Opacity = 0;
            Finished();
            return;
        }
        var leave = Move(1, 0, Quick, Leaving);
        leave.Completed += (_, _) => Finished();
        Scrim.BeginAnimation(OpacityProperty, leave);
        if (sheet) PanelFrame.BeginAnimation(OpacityProperty, Move(1, 0, Quick, Leaving));
        else FrameShift.BeginAnimation(TranslateTransform.XProperty, Move(0, PanelFrame.ActualWidth * UiScale, Quick, Leaving));
    }

    void PanelLayer_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyPanelBounds();

    /// <summary>The frame lays out in unscaled units under the UI scale, so its caps are the window's size over the scale.</summary>
    void ApplyPanelBounds()
    {
        var sheet = _vm.PanelPresentation == PanelPresentation.Sheet;
        PanelFrame.MaxWidth = Math.Max(300, (PanelLayer.ActualWidth - 48) / UiScale);
        PanelFrame.MaxHeight = sheet ? Math.Max(160, (PanelLayer.ActualHeight - 48) / UiScale) : double.PositiveInfinity;
    }

    void Dim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _vm.ClosePanel();

    /// <summary>The tab is the user's pick; a command that shows another screen picks it by setting the view model.</summary>
    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedTab = Enum.Parse<MainTab>((string)((RadioButton)sender).Tag);
        UpdateChip();
    }

    RadioButton TabOf(MainTab tab) => tab switch
    {
        MainTab.Today => TodayTab,
        MainTab.Schedule => ScheduleTab,
        MainTab.Timers => CustomTab,
        _ => TodoTab,
    };

    /// <summary>The next boss shows in the top bar on every screen but Today, when the window is wide enough for it.</summary>
    void UpdateChip()
    {
        if (_vm is null) return;
        var wide = ActualWidth / UiScale >= 760;
        NextChip.Visibility = wide && _vm.SelectedTab != MainTab.Today && _vm.Today.Hero.HasNext ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The alerts menu opens under the bell on a click, not only on a right click.</summary>
    void Bell_Click(object sender, RoutedEventArgs e)
    {
        if (BellButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = BellButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
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

    /// <summary>Text size and Windows' text scaling, which every screen and panel is drawn at.</summary>
    double UiScale => GetValue(UiScaleProperty) is ScaleTransform { ScaleX: > 0 and var scale } ? scale : 1;

    /// <summary>The window grows and shrinks with the text, so each screen keeps its layout.</summary>
    void UiScaleChanged(ScaleTransform? previous)
    {
        ApplyPanelBounds();
        WindowChrome.GetWindowChrome(this).CaptionHeight = BaseCaptionHeight * UiScale;
        if (_source is null || previous is not { ScaleX: > 0 } || WindowState != WindowState.Normal)
        {
            QueuePlacementCheck();
            return;
        }
        var ratio = UiScale / previous.ScaleX;
        var current = new Size(Width, Height);
        var size = _scaledSize is { } asked && current == _appliedSize ? asked : current;
        var target = new Size(size.Width * ratio, size.Height * ratio);
        ApplyPlacement(WindowGeometry.Resize(new WindowRect(Left, Top, Width, Height), target.Width, target.Height));
        _scaledSize = target;
        _appliedSize = new Size(Width, Height);
    }

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
        Width *= UiScale;
        Height *= UiScale;
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
        MinWidth = Math.Min(BaseMinWidth * UiScale, work.Width);
        MinHeight = Math.Min(BaseMinHeight * UiScale, work.Height);
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
        const int WmDisplayChange = 0x007E, WmSettingChange = 0x001A, WmNcHitTest = 0x0084;
        const int HtMaxButton = 9;
        if (message is WmDisplayChange or WmSettingChange) QueuePlacementCheck();
        if (message == WmNcHitTest && MainContent.IsEnabled && MaximizeButton.IsVisible)
        {
            var packed = lParam.ToInt64();
            var point = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff));
            if (CaptionMaximize.Bounds(MaximizeButton).Contains(point))
            {
                handled = true;
                return new IntPtr(HtMaxButton);
            }
        }
        if (CaptionMaximize.Handle(MaximizeButton, message, wParam, lParam, ToggleMaximize))
        {
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
        if (!_services.IsQuitting && !_vm.RequestLeave(Close, runImmediately: false))
        {
            e.Cancel = true;
            base.OnClosing(e);
            return;
        }
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
