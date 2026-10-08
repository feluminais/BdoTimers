using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Tests;

public class PanelMotionTests
{
    static void WithWindow(Action<AppServices, MainViewModel, MainWindow> test) => WpfTest.Run(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Motion." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                WpfTest.Drain();
                test(services, main, window);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });

    static T Named<T>(Window window, string name) where T : class => (T)window.FindName(name);

    /// <summary>The layer is shown, the first frames draw it out of sight, and the motion then brings it in and leaves everything at rest.</summary>
    [Fact]
    public void A_panel_comes_in_after_it_has_been_drawn_and_the_background_goes_dim_once_it_is_there() => WithWindow((services, main, window) =>
    {
        var layer = Named<UIElement>(window, "PanelLayer");
        var scrim = Named<UIElement>(window, "Scrim");
        var frame = Named<UIElement>(window, "PanelFrame");
        var background = Named<UIElement>(window, "MainContent");
        var content = Named<ContentControl>(window, "PanelContent");
        Assert.Equal(Visibility.Collapsed, layer.Visibility);

        main.OpenSettingsCommand.Execute(null);

        Assert.Equal(Visibility.Visible, layer.Visibility);
        Assert.IsType<SettingsPanel>(content.Content);
        if (SystemParameters.ClientAreaAnimation)
        {
            Assert.Equal(0, scrim.Opacity);
            Assert.Equal(0, frame.Opacity);
            Assert.True(background.IsEnabled);
        }
        WpfTest.Wait(900);
        Assert.Equal(1, scrim.Opacity);
        Assert.Equal(1, frame.Opacity);
        Assert.False(scrim.HasAnimatedProperties);
        Assert.False(frame.HasAnimatedProperties);
        Assert.False(background.IsEnabled);

        main.ClosePanel();
        WpfTest.Wait(900);
        Assert.Equal(Visibility.Collapsed, layer.Visibility);
        Assert.Null(content.Content);
        Assert.Equal(0, scrim.Opacity);
        Assert.Equal(1, frame.Opacity);
        Assert.True(background.IsEnabled);
    });

    /// <summary>The frame's caps come from the window's size, which a collapsed layer doesn't have yet.</summary>
    [Fact]
    public void A_drawer_is_440_wide_and_a_sheet_760_by_560_every_time_they_open_and_a_narrow_window_shrinks_the_sheet() => WithWindow((services, main, window) =>
    {
        var frame = Named<FrameworkElement>(window, "PanelFrame");
        for (var round = 0; round < 2; round++)
        {
            main.OpenPanel(new FollowingPanelViewModel(services, main));
            WpfTest.Drain();
            Assert.Equal(440, frame.ActualWidth);
            main.ClosePanel();
            WpfTest.Wait(900);
            main.OpenSettingsCommand.Execute(null);
            WpfTest.Drain();
            Assert.Equal(760, frame.ActualWidth);
            Assert.Equal(560, frame.ActualHeight);
            main.ClosePanel();
            WpfTest.Wait(900);
        }
        window.Width = 700;
        WpfTest.Drain();

        main.OpenSettingsCommand.Execute(null);
        WpfTest.Drain();

        Assert.InRange(frame.ActualWidth, 301, 700 - 48);
    });

    [Fact]
    public void The_background_comes_back_as_the_panel_starts_to_leave() => WithWindow((services, main, window) =>
    {
        var background = Named<UIElement>(window, "MainContent");
        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(900);
        Assert.False(background.IsEnabled);

        main.ClosePanel();
        WpfTest.Drain();

        Assert.True(background.IsEnabled);
    });

    [Fact]
    public void A_panel_that_replaces_another_keeps_the_background_as_it_is() => WithWindow((services, main, window) =>
    {
        var background = Named<UIElement>(window, "MainContent");
        var content = Named<ContentControl>(window, "PanelContent");
        main.OpenPanel(new FollowingPanelViewModel(services, main));
        WpfTest.Wait(900);
        Assert.False(background.IsEnabled);
        var data = services.Timers.Current;
        var boss = data.Timers.First(t => t.IsBuiltIn);

        main.OpenPanel(new BossPanelViewModel(services, main, boss));
        WpfTest.Drain();

        Assert.IsType<BossPanel>(content.Content);
        Assert.False(background.IsEnabled);
        WpfTest.Wait(500);
        Assert.False(background.IsEnabled);
        Assert.Equal(1, Named<UIElement>(window, "Scrim").Opacity);
    });

    [Fact]
    public void A_panel_view_is_kept_and_starts_over_for_the_next_panel_of_its_kind() => WithWindow((services, main, window) =>
    {
        var content = Named<ContentControl>(window, "PanelContent");
        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);
        var view = Assert.IsType<SettingsPanel>(content.Content);
        ((RadioButton)view.FindName("DataNav")).IsChecked = true;
        ((TextBox)view.FindName("SearchBox")).Text = "diag";
        main.ClosePanel();
        WpfTest.Wait(900);
        Assert.Null(content.Content);
        Assert.Equal(1, window.IdlePanelViews);

        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);

        Assert.Same(view, content.Content);
        Assert.IsType<SettingsPanelViewModel>(view.DataContext);
        Assert.True(((RadioButton)view.FindName("GeneralNav")).IsChecked);
        Assert.Equal("", ((TextBox)view.FindName("SearchBox")).Text);
        Assert.Equal(0, ((ScrollViewer)view.FindName("Scroller")).VerticalOffset);
        Assert.Equal(0, window.IdlePanelViews);
    });

    /// <summary>The view is the same, so a panel would otherwise open at the place the last one was scrolled to.</summary>
    [Fact]
    public void A_panel_starts_at_its_top_whatever_the_last_one_in_its_view_was_scrolled_to() => WithWindow((services, main, window) =>
    {
        window.Height = 380;
        WpfTest.Drain();
        var data = services.Timers.Current;
        var bosses = data.Timers.Where(t => BossRegions.IsSelected(data, t)).Take(2).ToArray();
        var content = Named<ContentControl>(window, "PanelContent");
        ScrollViewer Scroller() => PanelFocusScope.Descendants((DependencyObject)content.Content).OfType<ScrollViewer>().First();

        main.OpenPanel(new BossPanelViewModel(services, main, bosses[0]));
        WpfTest.Wait(500);
        var view = content.Content;
        Scroller().ScrollToBottom();
        WpfTest.Drain();
        Assert.True(Scroller().VerticalOffset > 0, "the panel is taller than the window");

        main.OpenPanel(new BossPanelViewModel(services, main, bosses[1]));
        WpfTest.Wait(300);
        Assert.Same(view, content.Content);
        Assert.Equal(0, Scroller().VerticalOffset);

        Scroller().ScrollToBottom();
        WpfTest.Drain();
        main.ClosePanel();
        WpfTest.Wait(700);
        main.OpenPanel(new BossPanelViewModel(services, main, bosses[0]));
        WpfTest.Wait(500);
        Assert.Same(view, content.Content);
        Assert.Equal(0, Scroller().VerticalOffset);
    });

    [Fact]
    public void Settings_open_at_the_boss_regions_when_asked_to_and_on_General_otherwise() => WithWindow((services, main, window) =>
    {
        var content = Named<ContentControl>(window, "PanelContent");
        main.OpenPanel(new SettingsPanelViewModel(services) { OpenAt = "Bosses" });
        WpfTest.Wait(500);
        var view = Assert.IsType<SettingsPanel>(content.Content);
        Assert.True(((RadioButton)view.FindName("BossesNav")).IsChecked);
        main.ClosePanel();
        WpfTest.Wait(900);

        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);

        Assert.Same(view, content.Content);
        Assert.True(((RadioButton)view.FindName("GeneralNav")).IsChecked);
    });

    [Fact]
    public void The_overlay_previews_only_while_the_Overlay_options_are_in_view() => WithWindow((services, main, window) =>
    {
        var content = Named<ContentControl>(window, "PanelContent");
        main.OpenOverlaySettings();
        WpfTest.Wait(500);
        var view = Assert.IsType<SettingsPanel>(content.Content);
        var settings = Assert.IsType<SettingsPanelViewModel>(main.Panel);
        Assert.True(((RadioButton)view.FindName("OverlayNav")).IsChecked);
        Assert.True(settings.Overlay.Previewing);

        ((RadioButton)view.FindName("AlertsNav")).IsChecked = true;
        Assert.False(settings.Overlay.Previewing);
        ((TextBox)view.FindName("SearchBox")).Text = "opacity";
        Assert.True(settings.Overlay.Previewing);

        main.ClosePanel();
        Assert.False(settings.Overlay.Previewing);
    });

    [Fact]
    public void Asking_for_Overlay_again_or_for_its_region_switches_the_open_Settings_and_keeps_its_edits() => WithWindow((services, main, window) =>
    {
        var content = Named<ContentControl>(window, "PanelContent");
        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);
        var view = Assert.IsType<SettingsPanel>(content.Content);
        var settings = Assert.IsType<SettingsPanelViewModel>(main.Panel);
        settings.CloseToTray = !settings.CloseToTray;

        main.OpenOverlaySettings();
        Assert.Same(settings, main.Panel);
        Assert.False(main.AskingDiscard);
        Assert.True(settings.HasChanges);
        Assert.True(((RadioButton)view.FindName("OverlayNav")).IsChecked);
        Assert.True(settings.Overlay.Previewing);

        settings.Overlay.OpenRegionSettingsCommand.Execute(null);
        Assert.True(((RadioButton)view.FindName("BossesNav")).IsChecked);
        Assert.False(settings.Overlay.Previewing);
    });

    /// <summary>Rows of the other categories would be laid out for nothing, and seen for a moment before the filter hid them.</summary>
    [Fact]
    public void Settings_start_with_only_the_first_category_showing() => WpfTest.Run(() =>
    {
        var panel = new SettingsPanel();
        var items = (StackPanel)panel.FindName("SettingsItems");
        var shown = items.Children.OfType<FrameworkElement>().Where(item => item.Visibility == Visibility.Visible).ToArray();
        Assert.NotEmpty(shown);
        Assert.All(shown, item => Assert.Equal("General", SettingsFilter.GetCategory(item)));
        Assert.Contains(items.Children.OfType<FrameworkElement>(), item => SettingsFilter.GetCategory(item) == "Alerts");
        Assert.Equal(Visibility.Collapsed, ((UIElement)panel.FindName("NoResults")).Visibility);
    });

    [Fact]
    public void Panel_views_are_built_while_the_window_is_idle_and_not_while_a_panel_is_open() => WithWindow((services, main, window) =>
    {
        var screens = new[] { "TodayScreen", "ScheduleScreen", "CustomScreen", "TodoScreen" };
        var before = screens.Select(name => Named<UIElement>(window, name).Visibility).ToArray();
        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);
        window.StartWarmUp();
        WpfTest.Wait(600);
        Assert.Equal(0, window.IdlePanelViews);

        main.ClosePanel();
        for (var i = 0; i < 100 && window.IdlePanelViews < 6; i++) WpfTest.Wait(100);

        Assert.Equal(6, window.IdlePanelViews);
        Assert.Equal(before, screens.Select(name => Named<UIElement>(window, name).Visibility));
    });

    [Fact]
    public void A_warmed_up_panel_view_is_used_when_the_panel_opens() => WithWindow((services, main, window) =>
    {
        window.BuildPanelView(typeof(FollowingPanelViewModel));
        Assert.Equal(1, window.IdlePanelViews);
        var content = Named<ContentControl>(window, "PanelContent");

        main.OpenPanel(new FollowingPanelViewModel(services, main));
        WpfTest.Wait(500);

        Assert.IsType<FollowingPanel>(content.Content);
        Assert.Equal(0, window.IdlePanelViews);
    });

    /// <summary>A view built while the window is idle is not in the window yet, and its buttons must still reach it when pressed.</summary>
    [Fact]
    public void A_panel_built_ahead_of_time_closes_from_its_close_and_Done_buttons_and_Save() => WithWindow((services, main, window) =>
    {
        void Press(Func<Button, bool> match)
        {
            var button = PanelFocusScope.Descendants(Named<ContentControl>(window, "PanelContent")).OfType<Button>().First(b => b.IsVisible && match(b));
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            WpfTest.Wait(900);
        }
        foreach (var type in new[] { typeof(FollowingPanelViewModel), typeof(SettingsPanelViewModel) }) window.BuildPanelView(type);
        WpfTest.Drain();

        main.OpenPanel(new FollowingPanelViewModel(services, main));
        WpfTest.Wait(500);
        Press(b => b.ToolTip is "Close");
        Assert.Null(main.Panel);

        main.OpenPanel(new FollowingPanelViewModel(services, main));
        WpfTest.Wait(500);
        Press(b => Equals(b.Content, "Done"));
        Assert.Null(main.Panel);

        WpfTest.WithoutVoice(services);
        main.OpenSettingsCommand.Execute(null);
        WpfTest.Wait(500);
        Press(b => Equals(b.Content, "Save"));
        Assert.Null(main.Panel);
    });

    [Fact]
    public void A_screen_eases_in_when_its_tab_is_picked_and_ends_at_rest() => WithWindow((services, main, window) =>
    {
        var screen = Named<FrameworkElement>(window, "ScheduleScreen");
        Named<RadioButton>(window, "ScheduleTab").IsChecked = true;
        WpfTest.Drain();
        if (SystemParameters.ClientAreaAnimation) Assert.Equal(0, screen.Opacity);

        WpfTest.Wait(700);

        Assert.Equal(1, screen.Opacity);
        Assert.False(screen.HasAnimatedProperties);
        Assert.Equal(0, screen.RenderTransform.Value.OffsetY);
    });
}
