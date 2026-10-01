using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class OverlayPresenceTests
{
    static readonly OverlaySnapshot Clock = new() { Clock = true };
    static readonly OverlaySnapshot Empty = new();
    static readonly OverlaySnapshot PopUp = Clock with
    {
        PopUps = [new UpcomingItem(new TimerDef { Name = "Bread" }, T0.AddMinutes(3))], HasDuePopUp = true,
    };
    static readonly OverlaySettings Defaults = new();
    static readonly OverlaySettings TimedShow = new() { ShowOnHotkey = true, ShowSeconds = 10 };
    static readonly OverlayPresence Idle = new();

    [Fact]
    public void Hidden_by_default() => Assert.False(Idle.IsVisible(Defaults, T0, Clock, previewing: false));

    [Fact]
    public void Always_show_keeps_it_up() =>
        Assert.True(Idle.IsVisible(Defaults with { AlwaysShow = true }, T0, Clock, previewing: false));

    [Fact]
    public void A_due_pop_up_brings_it_up() => Assert.True(Idle.IsVisible(Defaults, T0, PopUp, previewing: false));

    [Fact]
    public void Preview_shows_it_even_with_nothing_to_draw() => Assert.True(Idle.IsVisible(Defaults, T0, Empty, previewing: true));

    [Fact]
    public void Nothing_to_draw_stays_hidden_outside_preview() =>
        Assert.False(Idle.IsVisible(Defaults with { AlwaysShow = true }, T0, Empty, previewing: false));

    [Fact]
    public void Off_hides_everything() =>
        Assert.False(Idle.IsVisible(Defaults with { Enabled = false, AlwaysShow = true }, T0, PopUp, previewing: true));

    [Fact]
    public void A_show_press_shows_it_for_its_seconds()
    {
        var shown = Idle.PressShow(TimedShow, T0);

        Assert.True(shown.IsVisible(TimedShow, T0.AddSeconds(9), Clock, previewing: false));
        Assert.False(shown.IsVisible(TimedShow, T0.AddSeconds(10), Clock, previewing: false));
    }

    [Fact]
    public void A_second_press_hides_it_early_and_a_later_one_starts_again()
    {
        var hidden = Idle.PressShow(TimedShow, T0).PressShow(TimedShow, T0.AddSeconds(3));
        Assert.False(hidden.IsShowing(T0.AddSeconds(3)));

        var again = Idle.PressShow(TimedShow, T0).PressShow(TimedShow, T0.AddSeconds(20));
        Assert.True(again.IsShowing(T0.AddSeconds(21)));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void A_show_press_is_ignored_while_the_overlay_or_the_feature_is_off_or_always_shown(
        bool enabled, bool showOnHotkey, bool alwaysShow)
    {
        var settings = new OverlaySettings { Enabled = enabled, ShowOnHotkey = showOnHotkey, AlwaysShow = alwaysShow };
        Assert.Same(Idle, Idle.PressShow(settings, T0));
    }

    [Fact]
    public void Visible_only_where_it_may_show_judged_without_the_content()
    {
        foreach (var enabled in new[] { false, true })
        foreach (var alwaysShow in new[] { false, true })
        foreach (var presence in new[] { Idle, new OverlayPresence(T0.AddSeconds(5)) })
        foreach (var previewing in new[] { false, true })
        foreach (var content in new[] { Empty, Clock, PopUp })
        {
            var settings = TimedShow with { Enabled = enabled, AlwaysShow = alwaysShow };
            if (presence.IsVisible(settings, T0, content, previewing))
                Assert.True(presence.MayShow(settings, T0, previewing, content.HasDuePopUp));
        }
    }

    [Fact]
    public void May_show_is_ruled_out_when_off_or_when_nothing_holds_it_up_and_no_pop_up_can_be_due()
    {
        Assert.False(Idle.MayShow(Defaults, T0, previewing: false, popUpMayBeDue: false));
        Assert.False(Idle.MayShow(Defaults with { Enabled = false, AlwaysShow = true }, T0, previewing: true, popUpMayBeDue: true));
        Assert.True(Idle.MayShow(Defaults, T0, previewing: false, popUpMayBeDue: true));
        Assert.True(Idle.MayShow(Defaults with { AlwaysShow = true }, T0, previewing: false, popUpMayBeDue: false));
        Assert.True(Idle.MayShow(Defaults, T0, previewing: true, popUpMayBeDue: false));
        Assert.True(Idle.PressShow(TimedShow, T0).MayShow(TimedShow, T0.AddSeconds(1), previewing: false, popUpMayBeDue: false));
    }

    [Fact]
    public void Always_show_ends_a_timed_show_so_it_doesnt_come_back_when_turned_off()
    {
        var settled = Idle.PressShow(TimedShow, T0).Settle(TimedShow with { AlwaysShow = true });

        Assert.False(settled.IsVisible(TimedShow, T0.AddSeconds(1), Clock, previewing: false));
    }
}
