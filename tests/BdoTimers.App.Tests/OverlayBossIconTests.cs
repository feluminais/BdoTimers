using System.IO;
using System.Windows.Controls;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using IconPath = System.Windows.Shapes.Path;

namespace BdoTimers.App.Tests;

public class OverlayBossIconTests
{
    [Theory]
    [InlineData(OverlayLayout.List)]
    [InlineData(OverlayLayout.Card)]
    [InlineData(OverlayLayout.Bar)]
    public void Icons_replace_boss_names_and_toggle_back_in_every_layout(OverlayLayout layout) => WpfTest.Run(() =>
    {
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        TimerDef Boss(string name) => new() { Name = name, IsBuiltIn = true };
        var content = new OverlaySnapshot
        {
            Previous = new(now.AddMinutes(-12), [Boss("Karanda")], true),
            Next = new(now.AddMinutes(47), [Boss("Nouver"), Boss("Kutum")], false),
            PopUps = [new(Boss("Kzarka"), now.AddMinutes(5)), new(new() { Name = "Nouver" }, now.AddMinutes(10))],
        };
        var settings = new OverlaySettings { Layout = layout, BossIcons = true };
        Assert.False(new OverlaySettings().BossIcons);
        var model = new OverlayViewModel(new ArtLibrary(Path.GetTempPath()));
        model.Update(content, settings, now, false);
        Assert.Equal(new[] { "Nouver", "Kutum" }, model.Next!.Icons!.Select(i => i.Name));
        Assert.NotNull(model.PopUps[0].Icons);
        Assert.Null(model.PopUps[1].Icons); // A custom timer with a boss's name stays text.
        var icons = model.Next.Icons;
        model.Update(content, settings, now.AddSeconds(1), false);
        Assert.Same(icons, model.Next.Icons);

        var window = new OverlayWindow(model);
        try
        {
            window.Show();
            WpfTest.Drain();
            var drawn = PanelFocusScope.Descendants(window).OfType<IconPath>()
                .Where(p => p.IsVisible && p.DataContext is BossIcon).ToList();
            Assert.Equal(4, drawn.Count);
            Assert.All(drawn, p => Assert.NotNull(p.Stroke));
            Assert.DoesNotContain(PanelFocusScope.Descendants(window).OfType<TextBlock>(),
                t => t.IsVisible && t.Text == "Nouver · Kutum");
            UiCapture.Save(window, $"overlay-icons-{layout}.png");

            model.Update(content, settings with { BossIcons = false }, now, false);
            WpfTest.Drain();
            Assert.Null(model.Next.Icons);
            Assert.Contains(PanelFocusScope.Descendants(window).OfType<TextBlock>(),
                t => t.IsVisible && t.Text == "Nouver · Kutum");
            Assert.DoesNotContain(PanelFocusScope.Descendants(window).OfType<IconPath>(),
                p => p.IsVisible && p.DataContext is BossIcon);

            model.Update(new OverlaySnapshot { Next = new(now.AddMinutes(47), [Boss("Unknown · boss")], false) }, settings, now, false);
            WpfTest.Drain();
            Assert.Null(Assert.Single(model.Next.Icons!).Outline);
            Assert.Contains(PanelFocusScope.Descendants(window).OfType<TextBlock>(),
                t => t.IsVisible && t.Text == "Unknown · boss");

            model.Update(new OverlaySnapshot { Next = new(now.AddMinutes(47), [Boss("Nouver · Kutum")], false) }, settings, now, false);
            Assert.Null(Assert.Single(model.Next.Icons!).Outline);
            model.Update(content, settings, now, false);
            Assert.Equal(2, model.Next.Icons!.Count);

            model.Update(new OverlaySnapshot(), settings, now, true);
            Assert.NotNull(Assert.Single(model.Previous!.Icons!).Outline);
            Assert.NotNull(Assert.Single(model.Next!.Icons!).Outline);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Every_bundled_boss_has_a_distinct_frozen_icon() => WpfTest.Run(() =>
    {
        var names = new[] { "Nouver", "Kzarka", "Karanda", "Kutum", "Offin", "Garmoth", "Vell", "Quint", "Muraka",
            "Golden Pig King", "Sangoon", "Bulgasal", "Uturi" };
        var icons = names.Select(BossIcon.For).ToList();
        Assert.All(icons, icon => { Assert.NotNull(icon.Outline); Assert.True(icon.Outline.IsFrozen); });
        Assert.Equal(names.Length, icons.Select(i => i.Outline!.ToString()).Distinct().Count());
        Assert.Same(icons[0].Outline, BossIcon.For(" nouver ").Outline);
    });
}
