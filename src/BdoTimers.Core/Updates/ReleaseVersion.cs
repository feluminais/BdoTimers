using System.Globalization;

namespace BdoTimers.Core.Updates;

/// <summary>Stable product versions: two to four numeric components, with an optional v prefix and build metadata.</summary>
public static class ReleaseVersion
{
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrEmpty(text)) return false;
        if (text[0] is 'v' or 'V') text = text[1..];
        var metadata = text.IndexOf('+');
        if (metadata >= 0)
        {
            var suffix = text[(metadata + 1)..];
            if (suffix.Length == 0 || suffix.Split('.').Any(part => part.Length == 0
                || part.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))) return false;
            text = text[..metadata];
        }
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 4) return false;
        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || parts[i].Any(c => !char.IsAsciiDigit(c))
                || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
        }
        // Version treats absent build/revision components as -1; product versions need trailing zero equivalence.
        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }
}
