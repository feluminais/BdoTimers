using System.Text.RegularExpressions;

namespace BdoTimers.Core.Text;

/// <summary>A word or phrase and the spelling the voice should read instead, e.g. "Kzarka" read as "Kazaarka".</summary>
public sealed record Respelling(string Word, string SayAs);

/// <summary>
/// Respells names before they are spoken, so any voice says them the way players do. Speech engines guess made-up
/// names from their spelling; a respelling steers the guess with ordinary English letters.
/// </summary>
public static class Pronunciation
{
    /// <summary>
    /// Built-in boss respellings, checked against the phonemes espeak-ng produces for the Kokoro voices (US and UK).
    /// Bosses missing here are already read correctly.
    /// </summary>
    public static IReadOnlyList<Respelling> BossNames { get; } =
    [
        new("Kzarka", "Kazaarka"),     // ka-ZAR-kuh, not KAY-zar-ka
        new("Uturi", "Oo-too-ree"),    // oo-TOO-ree, not YOO-cher-ee
        new("Bulgasal", "Bulgahsal"),  // BUL-gah-sal, not BUL-gay-zal
        new("Muraka", "Moo-raka"),     // moo-RAH-kuh, not myoo-RAH-kuh
        new("Kutum", "Kootum"),        // KOO-tum, not KYOO-tum
    ];

    static readonly Dictionary<string, string> Map = BossNames.ToDictionary(r => r.Word, r => r.SayAs, StringComparer.OrdinalIgnoreCase);

    static readonly Regex Words = new(
        $@"(?<!\w)(?:{string.Join("|", BossNames.Select(r => Regex.Escape(r.Word)))})(?!\w)", RegexOptions.IgnoreCase);

    /// <summary>Replaces whole words (case-insensitive) with their respellings.</summary>
    public static string Apply(string text) => Words.Replace(text, m => Map[m.Value]);
}
