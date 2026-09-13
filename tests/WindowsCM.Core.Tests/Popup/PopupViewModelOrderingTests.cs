// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class PopupViewModelOrderingTests
{
    private sealed class FakeHistoryStore : IHistoryStore
    {
        private readonly List<ClipboardItem> _items = [];

        public FakeHistoryStore(IEnumerable<ClipboardItem> items) => _items.AddRange(items);

        public ClipboardItem AddOrUpdate(ClipboardItem item) => item;
        public IReadOnlyList<ClipboardItem> List() => _items;
        public long TryUpdateContent(long id, ItemKind kind, string content) => id;
        public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true) => 0;
        public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow, bool protectPinned = true, bool protectTagged = true) => 0;
        public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
            ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false) => _items;
        public void RefreshDate(long id, DateTime utcNow) { }
        public bool Delete(long id) => false;
        public void SetPinned(long id, bool pinned) { }
        public void SetTag(long id, string? tag) { }
        public void SetTitle(long id, string? title) { }
        public void SetMetadata(long id, string? metadataJson) { }
        public void SetMetadataAndTitle(long id, string? metadataJson, string? title) { }
        public void Dispose() { }
    }

    private static ClipboardItem MakeItem(long id) => new(
        ItemKind.Text,
        $"Item {id}",
        false,
        null,
        DateTime.UtcNow.AddMinutes(-id),
        null,
        null,
        id);

    [Fact]
    public void DefaultOrdering_RecentAtStart_SelectsFirstItem()
    {
        var store = new FakeHistoryStore([MakeItem(1), MakeItem(2), MakeItem(3)]);
        var model = new PopupViewModel(store);

        Assert.True(model.RecentAtStart);
        Assert.Equal(0, model.SelectedIndex);
        Assert.Equal(1, model.SelectedItem?.Id);
    }

    [Fact]
    public void RecentAtEnd_ReversesVisibleItems_SelectsLastItem()
    {
        var store = new FakeHistoryStore([MakeItem(1), MakeItem(2), MakeItem(3)]);
        var model = new PopupViewModel(store);

        model.SetItemOrdering(recentAtStart: false);

        Assert.False(model.RecentAtStart);
        // Reversed: 3, 2, 1
        Assert.Equal(3, model.VisibleItems[0].Id);
        Assert.Equal(2, model.VisibleItems[1].Id);
        Assert.Equal(1, model.VisibleItems[2].Id);
        // Selected index is the newest item (index 2)
        Assert.Equal(2, model.SelectedIndex);
        Assert.Equal(1, model.SelectedItem?.Id);
    }

    [Fact]
    public void RecentAtEnd_Navigation_MovesPreviousAndNext()
    {
        var store = new FakeHistoryStore([MakeItem(1), MakeItem(2), MakeItem(3)]);
        var model = new PopupViewModel(store);
        model.SetItemOrdering(recentAtStart: false);

        // Starts at index 2 (Item 1)
        Assert.Equal(2, model.SelectedIndex);

        // MovePrevious moves to index 1 (Item 2)
        model.MovePrevious();
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(2, model.SelectedItem?.Id);

        // MoveNext moves back to index 2 (Item 1)
        model.MoveNext();
        Assert.Equal(2, model.SelectedIndex);
        Assert.Equal(1, model.SelectedItem?.Id);
    }
}
