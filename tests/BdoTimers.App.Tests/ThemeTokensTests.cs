using System.Windows;
using System.Windows.Media;
using BdoTimers.App.Theme;

namespace BdoTimers.App.Tests;

public class ThemeTokensTests
{
    static ResourceDictionary Tokens() => new() { Source = new Uri("/BdoTimers;component/Theme/Tokens.xaml", UriKind.Relative) };

    [Fact]
    public void Every_brush_token_is_replaced_in_high_contrast() => WpfTest.Run(() =>
    {
        var tokens = Tokens();
        var brushes = tokens.Keys.Cast<object>().OfType<string>().Where(k => tokens[k] is Brush).ToList();
        Assert.NotEmpty(brushes);
        Assert.Empty(brushes.Except(ThemePreferences.PaletteKeys));
    });

    [Fact]
    public void Text_colours_keep_their_contrast_on_every_card() => WpfTest.Run(() =>
    {
        var tokens = Tokens();
        double L(string key) => Luminance(((SolidColorBrush)tokens[key]).Color);
        double Ratio(string a, string b) => (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
        foreach (var card in new[] { "CardBrush", "IndigoCardBrush", "ForestCardBrush", "PanelBrush" })
        {
            Assert.True(Ratio("TextBrush", card) >= 12, $"text on {card}");
            Assert.True(Ratio("SubtleBrush", card) >= 7, $"secondary on {card}");
            Assert.True(Ratio("PastBrush", card) >= 3.5, $"dim on {card}");
            Assert.True(Ratio("DangerBrush", card) >= 4.5, $"danger on {card}");
        }
        Assert.Equal(Colors.Black, ((SolidColorBrush)tokens["BgBrush"]).Color);
    });

    [Fact]
    public void Inter_regular_medium_and_semibold_resolve_from_the_bundled_family() => WpfTest.Run(() =>
    {
        var family = (FontFamily)Application.Current.FindResource("UiFont");
        foreach (var weight in new[] { FontWeights.Normal, FontWeights.Medium, FontWeights.SemiBold })
        {
            var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyphs));
            Assert.Contains(glyphs.FamilyNames.Values, name => name.StartsWith("Inter"));
            Assert.Equal(weight.ToOpenTypeWeight(), glyphs.Weight.ToOpenTypeWeight());
        }
    });

    static double Luminance(Color c)
    {
        static double Channel(byte v) { var s = v / 255d; return s <= .03928 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
    }
}
