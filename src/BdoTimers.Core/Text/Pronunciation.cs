using System.Text.RegularExpressions;

namespace BdoTimers.Core.Text;

/// <summary>
/// Respells names before they are spoken, so any voice says them the way players do. Speech engines guess made-up
/// names from their spelling; a respelling steers the guess with ordinary English letters.
/// </summary>
public static class Pronunciation
{
    /// <summary>
    /// Built-in boss names and the spellings the voice reads instead, checked against the phonemes espeak-ng produces for
    /// the Kokoro voices (US and UK). Bosses missing here are already read correctly.
    /// </summary>
    static readonly Dictionary<string, string> BossNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kzarka"] = "Kozaarka",     // k'-ZAR-kuh with the first vowel barely there; espeak-ng spells a bare "Kz" as KAY-zar-ka
        ["Uturi"] = "Oo-too-ree",    // oo-TOO-ree, not YOO-cher-ee
        ["Bulgasal"] = "Bulgahsal",  // BUL-gah-sal, not BUL-gay-zal
        ["Muraka"] = "Moo-raka",     // moo-RAH-kuh, not myoo-RAH-kuh
        ["Kutum"] = "Kootum",        // KOO-tum, not KYOO-tum
    };

    static readonly Regex Words = new(
        $@"(?<!\w)(?:{string.Join("|", BossNames.Keys.Select(Regex.Escape))})(?!\w)", RegexOptions.IgnoreCase);

    /// <summary>Replaces whole words (case-insensitive) with their respellings.</summary>
    public static string Apply(string text) => Words.Replace(text, m => BossNames[m.Value]);
}
