using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BdoTimers.App.Controls;

/// <summary>Searches of the element tree for code-behind.</summary>
public static class VisualTree
{
    /// <summary>The first <typeparamref name="T"/> that matches, from <paramref name="root"/> down, depth first.</summary>
    public static T? FindDescendant<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        if (root is T self && match(self)) return self;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindDescendant(VisualTreeHelper.GetChild(root, i), match) is { } found) return found;
        return null;
    }

    /// <summary>
    /// The nearest <typeparamref name="T"/> that matches, from <paramref name="element"/> up to, but not including,
    /// <paramref name="stopAt"/>. Text content such as a Run has no visual parent, so its logical one is followed.
    /// </summary>
    public static T? FindAncestor<T>(DependencyObject? element, DependencyObject stopAt, Func<T, bool>? match = null)
        where T : DependencyObject
    {
        for (; element is not null && element != stopAt; element = ParentOf(element))
            if (element is T found && (match is null || match(found))) return found;
        return null;
    }

    static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
