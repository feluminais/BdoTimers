using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Diagnostics;

namespace BdoTimers.App.Views;

/// <summary>
/// The panel layer: built panel views that are reused, motion that starts once the panel has been drawn, and the work of
/// building what is needed next while the window is idle.
/// </summary>
public partial class MainWindow
{
    // Gentle curves. The Windows entering curve (0, 0, 0, 1) puts about half the distance in its first two frames, which
    // reads as a snap, and its leaving twin holds still and then jumps.
    static readonly Duration ScrimIn = Ms(240), SlideIn = Ms(320), SheetIn = Ms(260), SlideOut = Ms(240), SheetOut = Ms(200), ScreenIn = Ms(200);
    static readonly IEasingFunction EaseOut = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    static readonly IEasingFunction EaseOutQuad = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseOut });
    static readonly IEasingFunction EaseInOut = Frozen(new SineEase { EasingMode = EasingMode.EaseInOut });

    /// <summary>The view for each kind of panel and the size it lays out at; other panels use their data templates.</summary>
    static readonly Dictionary<Type, (Func<FrameworkElement> Make, Size Size)> PanelViews = new()
    {
        [typeof(SettingsPanelViewModel)] = (() => new SettingsPanel(), new Size(760, 560)),
        [typeof(BossPanelViewModel)] = (() => new BossPanel(), new Size(440, 720)),
        [typeof(CustomPanelViewModel)] = (() => new CustomPanel(), new Size(440, 720)),
        [typeof(NewTimerPanelViewModel)] = (() => new NewTimerPanel(), new Size(440, 720)),
        [typeof(TodoListPanelViewModel)] = (() => new TodoListPanel(), new Size(440, 720)),
        [typeof(FollowingPanelViewModel)] = (() => new FollowingPanel(), new Size(440, 720)),
        [typeof(OverlayPanelViewModel)] = (() => new OverlayPanel(), new Size(440, 720)),
    };

    /// <summary>Views built earlier and not showing: a panel is a few hundred controls, so it is built once and bound to each new panel.</summary>
    readonly Dictionary<Type, Stack<FrameworkElement>> _idleViews = [];
    FrameworkElement? _shownView;
    Type? _shownType;
    // Bumped whenever a panel opens or leaves, or a screen starts to ease in, so a motion that was waiting to start knows it has been overtaken.
    int _motion;
    int _screenMotion;
    bool _leaving, _easing;
    readonly Queue<Action> _warmUp = [];
    bool _warmUpPending;

    internal int IdlePanelViews => _idleViews.Values.Sum(views => views.Count);

    static Duration Ms(int milliseconds) => new(TimeSpan.FromMilliseconds(milliseconds));

    static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    static DoubleAnimation Animate(double? from, double to, Duration length, IEasingFunction ease) =>
        new() { From = from, To = to, Duration = length, EasingFunction = ease };

    void ShowPanel(object? panel)
    {
        if (panel is null)
        {
            HidePanel();
            return;
        }
        var wasVisible = PanelLayer.Visibility == Visibility.Visible;
        var sheet = _vm.PanelPresentation == PanelPresentation.Sheet;
        var animate = SystemParameters.ClientAreaAnimation && !wasVisible;
        var token = ++_motion;
        _leaving = false;
        SettlePanelMotion(open: true);
        PanelFrame.IsHitTestVisible = true;
        // A panel that replaces another stays where it is; only a layer that was closed slides or fades in, and it starts
        // out of sight so that nothing shows before the panel has been built and drawn.
        if (animate)
        {
            Scrim.Opacity = 0;
            if (sheet)
            {
                PanelFrame.Opacity = 0;
                FrameShift.Y = 12;
            }
            else FrameShift.X = MainContent.ActualWidth;
        }
        ApplyPanelBounds();
        SetPanelContent(panel);
        PanelLayer.Visibility = Visibility.Visible;
        PanelLayer.UpdateLayout();
        // The background goes dim once the panel is in, so the dimming is the scrim's alone while it fades.
        _panelFocus.Open(disableBackground: !animate);
        if (!animate) return;
        AfterFrames(2, () =>
        {
            if (token != _motion || _vm.Panel is null) return;
            void Arrived()
            {
                if (token != _motion) return;
                SettlePanelMotion(open: true);
                _panelFocus.DisableBackground();
            }
            Scrim.BeginAnimation(OpacityProperty, Animate(0, 1, ScrimIn, EaseOutQuad));
            if (sheet)
            {
                var fade = Animate(0, 1, SheetIn, EaseOut);
                fade.Completed += (_, _) => Arrived();
                PanelFrame.BeginAnimation(OpacityProperty, fade);
                FrameShift.BeginAnimation(TranslateTransform.YProperty, Animate(12, 0, SheetIn, EaseOut));
            }
            else
            {
                var slide = Animate(PanelFrame.ActualWidth * UiScale, 0, SlideIn, EaseOut);
                slide.Completed += (_, _) => Arrived();
                FrameShift.BeginAnimation(TranslateTransform.XProperty, slide);
            }
        });
    }

    void HidePanel()
    {
        var sheet = _vm.PanelPresentation == PanelPresentation.Sheet;
        var animate = SystemParameters.ClientAreaAnimation && PanelLayer.Visibility == Visibility.Visible;
        var token = ++_motion;
        // It can't be clicked while it leaves; switching it off would restyle every control in it first.
        PanelFrame.IsHitTestVisible = false;
        // A panel opened in the same breath takes this one's place, and the background stays as it is.
        Dispatcher.BeginInvoke(DispatcherPriority.Send, () => { if (token == _motion) _panelFocus.Close(); });
        void Finished()
        {
            if (token != _motion) return;
            _leaving = false;
            SettlePanelMotion(open: false);
            PanelLayer.Visibility = Visibility.Collapsed;
            PanelContent.Content = null;
            if (_shownView is { } view && _shownType is { } type) KeepView(type, view);
            _shownView = null;
            _shownType = null;
            PanelFrame.IsHitTestVisible = true;
            ResumeWarmUp();
        }
        if (!animate)
        {
            Finished();
            return;
        }
        _leaving = true;
        // From wherever the motion has got to, so closing a panel that is still arriving doesn't jump.
        var leave = Animate(null, 0, sheet ? SheetOut : SlideOut, EaseInOut);
        leave.Completed += (_, _) => Finished();
        Scrim.BeginAnimation(OpacityProperty, leave);
        if (sheet)
        {
            PanelFrame.BeginAnimation(OpacityProperty, Animate(null, 0, SheetOut, EaseInOut));
            FrameShift.BeginAnimation(TranslateTransform.YProperty, Animate(null, 8, SheetOut, EaseInOut));
        }
        else FrameShift.BeginAnimation(TranslateTransform.XProperty, Animate(null, PanelFrame.ActualWidth * UiScale, SlideOut, EaseInOut));
    }

    /// <summary>Ends any motion and puts the layer at rest, shown or not.</summary>
    void SettlePanelMotion(bool open)
    {
        FrameShift.BeginAnimation(TranslateTransform.XProperty, null);
        FrameShift.BeginAnimation(TranslateTransform.YProperty, null);
        PanelFrame.BeginAnimation(OpacityProperty, null);
        Scrim.BeginAnimation(OpacityProperty, null);
        FrameShift.X = 0;
        FrameShift.Y = 0;
        PanelFrame.Opacity = 1;
        Scrim.Opacity = open ? 1 : 0;
    }

    /// <summary>Shows the panel in the view that is already up when it is of the same kind, else in one built earlier, else in a new one.</summary>
    void SetPanelContent(object panel)
    {
        var type = panel.GetType();
        var previous = _shownView;
        var previousType = _shownType;
        // A panel with no view of its own is shown as it is.
        var view = !PanelViews.ContainsKey(type) ? null : previousType == type ? previous : TakeView(type);
        if (view is null) PanelContent.Content = panel;
        else
        {
            view.DataContext = panel;
            if (ReferenceEquals(view, previous))
            {
                ScrollToTop(view);
                return;
            }
            PanelContent.Content = view;
        }
        _shownView = view;
        _shownType = view is null ? null : type;
        if (previous is not null && previousType is not null) KeepView(previousType, previous);
    }

    FrameworkElement TakeView(Type panelType) =>
        _idleViews.GetValueOrDefault(panelType) is { Count: > 0 } views ? views.Pop() : PanelViews[panelType].Make();

    /// <summary>A panel starts at its top, whatever the one that used the view before it was scrolled to.</summary>
    static void ScrollToTop(FrameworkElement view)
    {
        foreach (var scroller in PanelFocusScope.Descendants(view).OfType<ScrollViewer>()) scroller.ScrollToTop();
    }

    void KeepView(Type panelType, FrameworkElement view)
    {
        view.DataContext = null;
        ScrollToTop(view);
        if (!_idleViews.TryGetValue(panelType, out var views)) _idleViews[panelType] = views = [];
        // One spare is enough: only one panel shows at a time.
        if (views.Count < 1) views.Push(view);
    }

    /// <summary>Runs <paramref name="action"/> once the window has drawn <paramref name="frames"/> more frames, or soon anyway.</summary>
    void AfterFrames(int frames, Action action)
    {
        var done = false;
        var fallback = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
        void Run()
        {
            if (done) return;
            done = true;
            CompositionTarget.Rendering -= OnFrame;
            fallback.Stop();
            action();
        }
        void OnFrame(object? sender, EventArgs e)
        {
            if (--frames <= 0) Run();
        }
        fallback.Tick += (_, _) => Run();
        CompositionTarget.Rendering += OnFrame;
        fallback.Start();
    }

    /// <summary>A screen eases in when its tab is picked: a quick fade with a short rise, once it has been laid out. Not when Windows has animations off.</summary>
    void EaseIn(FrameworkElement screen)
    {
        if (!SystemParameters.ClientAreaAnimation || !screen.IsLoaded) return;
        if (screen.RenderTransform is not TranslateTransform rise) screen.RenderTransform = rise = new TranslateTransform();
        var token = ++_screenMotion;
        _easing = true;
        void Rest()
        {
            screen.BeginAnimation(OpacityProperty, null);
            rise.BeginAnimation(TranslateTransform.YProperty, null);
            screen.Opacity = 1;
            rise.Y = 0;
        }
        Rest();
        screen.Opacity = 0;
        rise.Y = 8;
        AfterFrames(2, () =>
        {
            if (token != _screenMotion)
            {
                Rest();
                return;
            }
            var fade = Animate(0, 1, ScreenIn, EaseOut);
            fade.Completed += (_, _) =>
            {
                if (token != _screenMotion) return;
                Rest();
                _easing = false;
                ResumeWarmUp();
            };
            screen.BeginAnimation(OpacityProperty, fade);
            rise.BeginAnimation(TranslateTransform.YProperty, Animate(8, 0, ScreenIn, EaseOut));
        });
    }

    /// <summary>
    /// Queues what the next click would otherwise wait for: each screen that is not showing is laid out once, and each panel's
    /// view is built and measured. The steps run one at a time while the window is idle and in view.
    /// </summary>
    internal void StartWarmUp()
    {
        foreach (var screen in new FrameworkElement[] { ScheduleScreen, CustomScreen, TodoScreen }) _warmUp.Enqueue(() => LayOutHidden(screen));
        foreach (var type in PanelViews.Keys) _warmUp.Enqueue(() => BuildPanelView(type));
        ResumeWarmUp();
    }

    /// <summary>A step takes as long as it takes, so none starts while the window is out of view or something is moving.</summary>
    bool CanWarmUp => IsVisible && WindowState != WindowState.Minimized && _vm.Panel is null && !_leaving && !_easing;

    /// <summary>Runs the next step once the dispatcher is idle. When a step can't run, this is called again once that changes.</summary>
    void ResumeWarmUp()
    {
        if (_warmUpPending || _warmUp.Count == 0 || !CanWarmUp) return;
        _warmUpPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            _warmUpPending = false;
            if (!CanWarmUp) return;
            try { _warmUp.Dequeue()(); }
            catch (Exception ex) { Log.Error("Couldn't build part of the interface ahead of time", ex); }
            ResumeWarmUp();
        });
    }

    /// <summary>Lays a screen that isn't showing out once, without it ever being seen, so its first showing is cheap.</summary>
    internal void LayOutHidden(FrameworkElement screen)
    {
        if (screen.IsVisible) return;
        var before = screen.Visibility;
        // Collapsed elements are not measured; hidden ones are.
        screen.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        try { screen.UpdateLayout(); }
        finally { screen.SetCurrentValue(VisibilityProperty, before); }
    }

    /// <summary>Builds one panel view and lays it out at its size, then keeps it for the next panel of its kind.</summary>
    internal void BuildPanelView(Type panelType)
    {
        if (_idleViews.GetValueOrDefault(panelType) is { Count: > 0 }) return;
        var (make, size) = PanelViews[panelType];
        var view = make();
        view.Measure(size);
        view.Arrange(new Rect(size));
        KeepView(panelType, view);
    }
}
