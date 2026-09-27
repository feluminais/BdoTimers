using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class ColorTests
{
    [Theory]
    [InlineData("#3A1417", 0x3A, 0x14, 0x17)]
    [InlineData("3a1417", 0x3A, 0x14, 0x17)]
    [InlineData("#fa0", 0xFF, 0xAA, 0x00)]
    [InlineData(" #0B0B0C ", 0x0B, 0x0B, 0x0C)]
    public void Hex_is_read(string text, int r, int g, int b)
    {
        Assert.True(RgbColor.TryParseHex(text, out var color));
        Assert.Equal(new RgbColor((byte)r, (byte)g, (byte)b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#1234567")]
    [InlineData(null)]
    public void Other_text_is_refused(string? text) => Assert.False(RgbColor.TryParseHex(text, out _));

    [Fact]
    public void Hex_is_written_upper_case_with_a_hash() => Assert.Equal("#3A1417", new RgbColor(0x3A, 0x14, 0x17).ToHex());

    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void Hsv_of_primaries(int r, int g, int b, double h, double s, double v)
    {
        var hsv = HsvColor.From(new RgbColor((byte)r, (byte)g, (byte)b));
        Assert.Equal(h, hsv.H, 3);
        Assert.Equal(s, hsv.S, 3);
        Assert.Equal(v, hsv.V, 3);
    }

    [Theory]
    [InlineData("#3A1417")]
    [InlineData("#B89A5E")]
    [InlineData("#141B2E")]
    [InlineData("#808080")]
    [InlineData("#FF00FF")]
    public void Hsv_round_trips(string hex)
    {
        Assert.True(RgbColor.TryParseHex(hex, out var rgb));
        Assert.Equal(rgb, HsvColor.From(rgb).ToRgb());
    }
}
