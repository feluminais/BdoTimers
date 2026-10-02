using System.Collections.ObjectModel;

namespace BdoTimers.App.ViewModels;

public static class CollectionSync
{
    /// <summary>
    /// Brings <paramref name="items"/> in line with <paramref name="wanted"/>: the item that <paramref name="matches"/> a
    /// wanted one is kept, moved into place and <paramref name="update"/>d, a wanted one with no item gets a new one, and
    /// the items left over are removed, all but the last <paramref name="keepLast"/>. Kept items keep their visuals, so
    /// only what changed is drawn again.
    /// </summary>
    public static void Sync<TItem, TWanted>(this ObservableCollection<TItem> items, IEnumerable<TWanted> wanted,
        Func<TItem, TWanted, bool> matches, Func<TWanted, TItem> create, Action<TItem, TWanted> update, int keepLast = 0)
    {
        var i = 0;
        foreach (var next in wanted)
        {
            var at = i;
            while (at < items.Count - keepLast && !matches(items[at], next)) at++;
            if (at == items.Count - keepLast) items.Insert(i, create(next));
            else
            {
                if (at != i) items.Move(at, i);
                update(items[i], next);
            }
            i++;
        }
        while (items.Count > i + keepLast) items.RemoveAt(i);
    }
}
