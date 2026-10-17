using System.IO;
using System.Windows.Controls;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.Tests;

public class OverlayNightClockTests
{
    [Theory]
    [InlineData(OverlayLayout.List, "2026-10-07T10:00:00Z", "01:40:00", "")]
    [InlineData(OverlayLayout.Card, "2026-10-07T10:00:00Z", "01:40:00", "")]
    [InlineData(OverlayLayout.Bar, "2026-10-07T03:50:00Z", "00:30:00", "")]
    public void The_countdown_sits_with_the_clocks_and_shows_what_comes_next(OverlayLayout layout, string at, string left, string icon) => WpfTest.Run(() =>
    {
        var now = DateTimeOffset.Parse(at);
        var settings = new OverlaySettings { Layout = layout, ShowGameTime = true, ShowNight = true };
        var model = new OverlayViewModel(new ArtLibrary(Path.GetTempPath()));
        model.Update(OverlayContent.Build(new AppData(), settings, now), settings, now, false);
        Assert.Equal(left, model.NightClock);

        var window = new OverlayWindow(model);
        try
        {
            window.Show();
            WpfTest.Drain();
            var texts = PanelFocusScope.Descendants(window).OfType<TextBlock>().Where(t => t.IsVisible).ToList();
            var time = texts.Single(t => t.Text == left);
            var glyph = texts.Where(t => t.Text is "" or "").ToList();
            Assert.Equal(2, glyph.Count);
            Assert.Equal(icon, glyph[1].Text);
            UiCapture.Save(window, $"overlay-night-{layout}.png");

            model.Update(OverlayContent.Build(new AppData(), settings with { ShowNight = false }, now), settings with { ShowNight = false }, now, false);
            WpfTest.Drain();
            Assert.Null(model.NightClock);
            Assert.DoesNotContain(PanelFocusScope.Descendants(window).OfType<TextBlock>(), t => t.IsVisible && t.Text == left);
            Assert.NotNull(time);
        }
        finally { window.Close(); }
    });
}
