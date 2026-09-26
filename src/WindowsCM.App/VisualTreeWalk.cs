// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WindowsCM.App;

// Ancestor lookup from a mouse event's OriginalSource. A click on the text
// of a code card lands on a Run (the syntax highlight), and
// VisualTreeHelper.GetParent throws for anything that is not a Visual: each
// click threw, and six in ten seconds ended the app (UnhandledErrorPolicy).
// Content elements climb through their logical parent (Run -> TextBlock).
internal static class VisualTreeWalk
{
    public static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
