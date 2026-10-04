using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BdoTimers.App.Art;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.Tests;

public class BossStripLayoutTests
{
    [Theory]
    [InlineData("Next", 360, 1, 2)]
    [InlineData("Previous", 230, 1.5, 2)]
    [InlineData("Followed by", 230, 1, 2)]
    [InlineData("Next", 360, 1.5, 3)]
    public void Shared_spawn_names_align_with_their_own_art_and_keep_timing_separate(string caption, double width, double scale, int count) => WpfTest.Run(() =>
    {
        var now = DateTimeOffset.Parse("2026-10-04T06:00:00Z");
        var art = new ArtLibrary(System.IO.Path.GetTempPath());
        var bosses = new[] { "Uturi", "Kzarka", "Golden Pig King" }.Take(count)
            .Select(name => new TimerDef { Name = name, IsBuiltIn = true }).ToArray();
        var opened = new List<Guid>();
        var model = new StripTileViewModel(caption, elapsed: caption == "Previous");
        model.Update(new SpawnGroup(now.AddHours(1), bosses, false), now, art, opened.Add);
        var card = CreateCard(model, caption, width, scale);
        var window = new Window { Content = card, SizeToContent = SizeToContent.WidthAndHeight };
        try
        {
            window.Show();
            WpfTest.Drain();
            var children = PanelFocusScope.Descendants(card).ToArray();
            var names = children.OfType<Button>().Where(b => b.IsVisible && b.DataContext is BossLink).ToArray();
            var pictures = children.OfType<FocusImage>().Where(p => p.IsVisible).ToArray();
            Assert.Equal(count, names.Length);
            Assert.Equal(count, pictures.Length);
            for (var index = 0; index < count; index++)
            {
                Assert.Same(model.Names[index], names[index].DataContext);
                Assert.Same(model.Names[index].Images.Single(), pictures[index].Picture);
                var nameBounds = Bounds(names[index], card);
                var pictureBounds = Bounds(pictures[index], card);
                Assert.InRange(nameBounds.Top, pictureBounds.Top - 1, pictureBounds.Bottom);
                Assert.InRange(nameBounds.Bottom, pictureBounds.Top, pictureBounds.Bottom + 1);
                Assert.InRange(Math.Abs((nameBounds.Top + nameBounds.Bottom - pictureBounds.Top - pictureBounds.Bottom) / 2), 0, 1);
                names[index].Command!.Execute(null);
            }
            Assert.Equal(bosses.Select(b => b.Id), opened);
            var clock = Assert.Single(children.OfType<TextBlock>(), t => t.IsVisible && t.Text == model.Clock);
            Assert.Single(children.OfType<TextBlock>(), t => t.IsVisible && t.Text == model.Label);
            foreach (var name in names) Assert.False(Bounds(name, card).IntersectsWith(Bounds(clock, card)));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Changing_between_single_and_shared_spawns_keeps_one_clickable_name_per_boss() => WpfTest.Run(() =>
    {
        var now = DateTimeOffset.Parse("2026-10-04T06:00:00Z");
        var art = new ArtLibrary(System.IO.Path.GetTempPath());
        TimerDef[] bosses = [new() { Name = "Uturi", IsBuiltIn = true }, new() { Name = "Kzarka", IsBuiltIn = true }];
        var model = new StripTileViewModel("Next", elapsed: false);
        var card = CreateCard(model, "Next", 360, 1);
        var window = new Window { Content = card, SizeToContent = SizeToContent.WidthAndHeight };
        try
        {
            window.Show();
            foreach (var count in new[] { 1, 2, 1 })
            {
                model.Update(new SpawnGroup(now.AddHours(count), bosses.Take(count).ToArray(), count == 2), now, art, _ => { });
                WpfTest.Drain();
                Assert.Equal(count > 1, model.HasMultipleBosses);
                Assert.Equal(count, PanelFocusScope.Descendants(card).OfType<Button>().Count(b => b.IsVisible && b.DataContext is BossLink));
                Assert.Equal(count, PanelFocusScope.Descendants(card).OfType<FocusImage>().Count(p => p.IsVisible));
            }
        }
        finally { window.Close(); }
    });

    static ContentControl CreateCard(StripTileViewModel model, string caption, double width, double scale) => new()
    {
        Content = model,
        ContentTemplate = (DataTemplate)new BossesView().Resources["StripTile"],
        Tag = caption,
        Width = width,
        Height = 116,
        LayoutTransform = new ScaleTransform(scale, scale),
    };

    static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
}
