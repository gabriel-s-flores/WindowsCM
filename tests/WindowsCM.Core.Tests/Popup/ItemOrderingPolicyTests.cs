// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class ItemOrderingPolicyTests
{
    private static ClipboardItem MakeItem(long id, DateTime capturedAt) => new(
        ItemKind.Text,
        $"Item {id}",
        false,
        null,
        capturedAt,
        null,
        null,
        id);

    [Fact]
    public void OrderItems_RecentAtStart_PreservesOrder()
    {
        var now = DateTime.UtcNow;
        var items = new List<ClipboardItem>
        {
            MakeItem(1, now),
            MakeItem(2, now.AddMinutes(-1)),
            MakeItem(3, now.AddMinutes(-2)),
        };

        var ordered = ItemOrderingPolicy.OrderItems(items, recentAtStart: true);

        Assert.Equal(3, ordered.Count);
        Assert.Equal(1, ordered[0].Id);
        Assert.Equal(2, ordered[1].Id);
        Assert.Equal(3, ordered[2].Id);
    }

    [Fact]
    public void OrderItems_RecentAtEnd_ReversesOrder()
    {
        var now = DateTime.UtcNow;
        var items = new List<ClipboardItem>
        {
            MakeItem(1, now),
            MakeItem(2, now.AddMinutes(-1)),
            MakeItem(3, now.AddMinutes(-2)),
        };

        var ordered = ItemOrderingPolicy.OrderItems(items, recentAtStart: false);

        Assert.Equal(3, ordered.Count);
        Assert.Equal(3, ordered[0].Id);
        Assert.Equal(2, ordered[1].Id);
        Assert.Equal(1, ordered[2].Id);
    }

    [Fact]
    public void OrderItems_EmptyList_ReturnsEmpty()
    {
        var items = new List<ClipboardItem>();

        var orderedTrue = ItemOrderingPolicy.OrderItems(items, recentAtStart: true);
        var orderedFalse = ItemOrderingPolicy.OrderItems(items, recentAtStart: false);

        Assert.Empty(orderedTrue);
        Assert.Empty(orderedFalse);
    }

    [Fact]
    public void GetInitialSelectedIndex_EmptyList_ReturnsNegativeOne()
    {
        Assert.Equal(-1, ItemOrderingPolicy.GetInitialSelectedIndex(0, recentAtStart: true));
        Assert.Equal(-1, ItemOrderingPolicy.GetInitialSelectedIndex(0, recentAtStart: false));
    }

    [Fact]
    public void GetInitialSelectedIndex_RecentAtStart_ReturnsZero()
    {
        Assert.Equal(0, ItemOrderingPolicy.GetInitialSelectedIndex(5, recentAtStart: true));
    }

    [Fact]
    public void GetInitialSelectedIndex_RecentAtEnd_ReturnsLastIndex()
    {
        Assert.Equal(4, ItemOrderingPolicy.GetInitialSelectedIndex(5, recentAtStart: false));
    }
}
