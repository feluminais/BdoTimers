using System.Globalization;

namespace BdoTimers.Core.Model;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Reads "#RGB" or "#RRGGBB" in any case; the # may be left out.</summary>
    public static bool TryParseHex(string? text, out RgbColor color)
    {
        color = default;
        var hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;
        color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>Hue 0-360, saturation and value 0-1: the colour picker's strip and square.</summary>
public readonly record struct HsvColor(double H, double S, double V)
{
    public static HsvColor From(RgbColor c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        var h = delta == 0 ? 0
            : max == r ? 60 * ((g - b) / delta)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return new HsvColor(h < 0 ? h + 360 : h, max == 0 ? 0 : delta / max, max);
    }

    public RgbColor ToRgb()
    {
        var sector = (H % 360 + 360) % 360 / 60;
        var chroma = V * S;
        var x = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = V - chroma;
        return new RgbColor(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    static byte ToByte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255);
}
