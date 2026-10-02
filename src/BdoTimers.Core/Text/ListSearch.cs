using System.Globalization;

namespace BdoTimers.Core.Text;

/// <summary>Finds a list entry from typed text, for lists whose entries share a prefix such as "(UTC+02:00)".</summary>
public static class ListSearch
{
    const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// The index of the first entry that starts with <paramref name="query"/>, else the first with a word starting with
    /// it, else the first containing it; -1 when none does. Case and accents are ignored, so "sao" finds "São Tomé".
    /// </summary>
    public static int Find(IReadOnlyList<string> entries, string query)
    {
        if (query.Length == 0) return -1;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var best = -1;
        var bestRank = int.MaxValue;
        for (var i = 0; i < entries.Count && bestRank > 0; i++)
        {
            var rank = Rank(compare, entries[i], query);
            if (rank < bestRank) (best, bestRank) = (i, rank);
        }
        return best;
    }

    /// <summary>0 for a prefix, 1 for a word start, 2 for anywhere else, <see cref="int.MaxValue"/> for no match.</summary>
    static int Rank(CompareInfo compare, string entry, string query)
    {
        var rank = int.MaxValue;
        for (var at = compare.IndexOf(entry, query, Loose); at >= 0; at = compare.IndexOf(entry, query, at + 1, Loose))
        {
            if (at == 0) return 0;
            if (!char.IsLetterOrDigit(entry[at - 1])) return 1;
            rank = 2;
        }
        return rank;
    }
}
