// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Popup;

public static class ItemOrderingPolicy
{
    public static IReadOnlyList<ClipboardItem> OrderItems(
        IReadOnlyList<ClipboardItem> items,
        bool recentAtStart)
    {
        if (items.Count <= 1 || recentAtStart)
        {
            return items;
        }

        var reversed = new List<ClipboardItem>(items.Count);
        for (var i = items.Count - 1; i >= 0; i--)
        {
            reversed.Add(items[i]);
        }
        return reversed;
    }

    public static int GetInitialSelectedIndex(int count, bool recentAtStart)
    {
        if (count <= 0)
        {
            return -1;
        }
        return recentAtStart ? 0 : count - 1;
    }
}
