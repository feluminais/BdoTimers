using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class OverlayMouseAvoidanceTests
{
    static readonly WindowRect Bounds = new(100, 100, 200, 80);

    [Theory]
    [InlineData(76, 140, true)]
    [InlineData(75, 140, false)]
    [InlineData(324, 140, true)]
    [InlineData(200, 76, true)]
    [InlineData(200, 204, true)]
    [InlineData(100, 100, true)]
    [InlineData(200, 140, true)]
    [InlineData(83, 83, false)]
    [InlineData(84, 84, true)]
    public void Approaching_any_edge_or_corner_uses_distance(double x, double y, bool near) =>
        Assert.Equal(near, new OverlayMouseAvoidance().Update(OverlayMouseProximity.Fade, Bounds, x, y).IsNear);

    [Fact]
    public void Exit_margin_prevents_flicker_and_resets_for_the_next_approach()
    {
        var state = new OverlayMouseAvoidance().Update(OverlayMouseProximity.Hide, Bounds, 80, 140);
        Assert.True(state.IsNear);
        state = state.Update(OverlayMouseProximity.Hide, Bounds, 60, 140);
        Assert.True(state.IsNear);
        state = state.Update(OverlayMouseProximity.Hide, Bounds, 59, 140);
        Assert.False(state.IsNear);
        Assert.False(state.Update(OverlayMouseProximity.Hide, Bounds, 70, 140).IsNear);
    }

    [Fact]
    public void Moving_or_resizing_the_overlay_uses_its_current_bounds()
    {
        var state = new OverlayMouseAvoidance(true);
        Assert.False(state.Update(OverlayMouseProximity.Hide, new(600, 100, 200, 80), 200, 140).IsNear);
        Assert.True(new OverlayMouseAvoidance().Update(OverlayMouseProximity.Fade, new(-1500, -200, 400, 180), -1080, -100).IsNear);
    }

    [Fact]
    public void Off_and_invalid_samples_release_proximity()
    {
        var state = new OverlayMouseAvoidance(true);
        Assert.False(state.Update(OverlayMouseProximity.Off, Bounds, 200, 140).IsNear);
        Assert.False(state.Update((OverlayMouseProximity)99, Bounds, 200, 140).IsNear);
        Assert.False(state.Update(OverlayMouseProximity.Hide, default, 200, 140).IsNear);
        Assert.False(state.Update(OverlayMouseProximity.Hide, Bounds, double.NaN, 140).IsNear);
        Assert.False(state.Update(OverlayMouseProximity.Hide, Bounds, 200, double.PositiveInfinity).IsNear);
    }

    [Theory]
    [InlineData(OverlayMouseProximity.Off, 1)]
    [InlineData(OverlayMouseProximity.Fade, .15)]
    [InlineData(OverlayMouseProximity.Hide, 0)]
    public void Modes_choose_whole_overlay_opacity(OverlayMouseProximity mode, double expected)
    {
        Assert.Equal(expected, new OverlayMouseAvoidance(true).Opacity(mode));
        Assert.Equal(1, new OverlayMouseAvoidance().Opacity(mode));
    }

    [Theory]
    [InlineData(OverlayMouseProximity.Off)]
    [InlineData(OverlayMouseProximity.Fade)]
    [InlineData(OverlayMouseProximity.Hide)]
    public void Settings_round_trip_and_old_settings_default_to_off(OverlayMouseProximity mode)
    {
        var settings = new AppSettings { Overlay = new() { MouseProximity = mode } };
        var saved = JsonSerializer.Serialize(settings, JsonDefaults.Options);
        var loaded = JsonSerializer.Deserialize<AppSettings>(saved, JsonDefaults.Options)!;
        Assert.Equal(mode, loaded.Overlay.MouseProximity);
        Assert.Equal(saved, JsonSerializer.Serialize(loaded, JsonDefaults.Options));
        Assert.Equal(OverlayMouseProximity.Off, JsonSerializer.Deserialize<AppSettings>("{\"overlay\":{}}", JsonDefaults.Options)!.Overlay.MouseProximity);
    }

    [Theory]
    [InlineData(OverlayMouseProximity.Fade)]
    [InlineData(OverlayMouseProximity.Hide)]
    public void Proximity_modes_do_not_extend_timed_shows_or_pop_ups(OverlayMouseProximity mode)
    {
        var settings = new OverlaySettings { MouseProximity = mode, ShowSeconds = 10 };
        var content = new OverlaySnapshot { Clock = true };
        var presence = new OverlayPresence().PressShow(settings, T0);
        Assert.True(presence.IsVisible(settings, T0.AddSeconds(9), content, false));
        Assert.False(presence.IsVisible(settings, T0.AddSeconds(10), content, false));
        Assert.True(new OverlayPresence().IsVisible(settings, T0, content with { HasDuePopUp = true }, false));
        Assert.False(new OverlayPresence().IsVisible(settings, T0, content, false));
    }
}
