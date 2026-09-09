// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

// In-memory IHistoryStore for the ViewModel state machine (spec Testing
// Decisions: popup ViewModel over a fake store). Search mirrors the SQLite
// semantics: case-insensitive body match with title fallback, newest first.
internal sealed class FakeHistoryStore : IHistoryStore
{
    private readonly List<ClipboardItem> _items = [];
    private long _nextId = 1;

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        var existing = _items.FirstOrDefault(i => i.Kind == item.Kind && i.Content == item.Content);
        if (existing is not null)
        {
            var bumped = existing with { CapturedAt = item.CapturedAt };
            _items[_items.IndexOf(existing)] = bumped;
            return bumped;
        }
        var saved = item with { Id = _nextId++ };
        _items.Add(saved);
        return saved;
    }

    public IReadOnlyList<ClipboardItem> List() =>
        _items.OrderByDescending(i => i.CapturedAt).ToList();

    public long TryUpdateContent(long id, ItemKind kind, string content)
    {
        var conflict = _items.FirstOrDefault(i => i.Id != id && i.Kind == kind && i.Content == content);
        if (conflict is not null)
        {
            return conflict.Id;
        }
        var at = _items.FindIndex(i => i.Id == id);
        if (at >= 0)
        {
            _items[at] = _items[at] with { Kind = kind, Content = content };
        }
        return -1;
    }

    public bool Delete(long id) => _items.RemoveAll(i => i.Id == id) > 0;

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true)
    {
        bool Protected(ClipboardItem i) =>
            keepProtected && ((protectPinned && i.Pinned) || (protectTagged && i.Tag is not null));
        return _items.RemoveAll(i => !Protected(i));
    }

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true)
    {
        var ordered = List().ToList();
        var victims = ordered.Skip(maxCount).ToList();
        if (maxAgeMinutes > 0)
        {
            victims.AddRange(ordered
                .Take(maxCount)
                .Where(i => i.CapturedAt < utcNow.AddMinutes(-maxAgeMinutes)));
        }
        var count = 0;
        foreach (var victim in victims.DistinctBy(i => i.Id))
        {
            if ((protectPinned && victim.Pinned) || (protectTagged && victim.Tag is not null))
            {
                continue;
            }
            count += _items.RemoveAll(i => i.Id == victim.Id);
        }
        return count;
    }

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false) =>
        _items
            .Where(i => string.IsNullOrEmpty(query)
                || i.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (i.Title is not null && i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Where(i => pinned is null || i.Pinned == pinned)
            .Where(i => !excludePinned || !i.Pinned)
            .Where(i => tag is null || i.Tag == tag)
            .Where(i => !excludeTagged || i.Tag is null)
            .Where(i => kind is null || i.Kind == kind)
            .OrderByDescending(i => i.CapturedAt)
            .ToList();

    public void RefreshDate(long id, DateTime utcNow)
    {
        var at = _items.FindIndex(i => i.Id == id);
        if (at >= 0)
        {
            _items[at] = _items[at] with { CapturedAt = utcNow };
        }
    }

    public void SetPinned(long id, bool pinned) => Mutate(id, i => i with { Pinned = pinned });
    public void SetTag(long id, string? tag) => Mutate(id, i => i with { Tag = tag });
    public void SetTitle(long id, string? title) => Mutate(id, i => i with { Title = title });

    private void Mutate(long id, Func<ClipboardItem, ClipboardItem> change)
    {
        var at = _items.FindIndex(i => i.Id == id);
        if (at >= 0)
        {
            _items[at] = change(_items[at]);
        }
    }

    // Fake holds no resources; Dispose satisfies the store interface.
    public void Dispose()
    {
    }
}

public sealed class PopupViewModelTests
{
    private static readonly DateTime T0 = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeHistoryStore _store = new();
    private PopupViewModel Subject() => new(_store);

    private ClipboardItem Save(
        string content,
        ItemKind kind = ItemKind.Text,
        bool pinned = false,
        string? tag = null,
        string? title = null,
        int minute = 0) =>
        _store.AddOrUpdate(new ClipboardItem(
            kind, content, pinned, tag, T0.AddMinutes(minute), null, title));

    [Fact]
    public void Initial_SelectsFirst_NewestFirst()
    {
        Save("old", minute: 0);
        Save("new", minute: 1);

        var vm = Subject();

        Assert.Equal(2, vm.VisibleItems.Count);
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Equal("new", vm.SelectedItem!.Content);
    }

    [Fact]
    public void EmptyStore_HasNoSelection()
    {
        var vm = Subject();

        Assert.Equal(-1, vm.SelectedIndex);
        Assert.Null(vm.SelectedItem);
        vm.MoveNext();
        vm.MovePrevious();
        vm.MoveFirst();
        vm.MoveLast();
        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public void MoveNext_WrapsAtEnd()
    {
        Save("a", minute: 0);
        Save("b", minute: 1);
        var vm = Subject(); // [b, a], selected b

        vm.MoveNext();
        Assert.Equal("a", vm.SelectedItem!.Content);
        vm.MoveNext();
        Assert.Equal("b", vm.SelectedItem!.Content);
    }

    [Fact]
    public void MovePrevious_WrapsAtStart()
    {
        Save("a", minute: 0);
        Save("b", minute: 1);
        var vm = Subject();

        vm.MovePrevious();
        Assert.Equal("a", vm.SelectedItem!.Content);
    }

    [Fact]
    public void MoveFirst_Last_Clamp()
    {
        Save("a", minute: 0);
        Save("b", minute: 1);
        Save("c", minute: 2);
        var vm = Subject(); // [c, b, a]

        vm.MoveLast();
        Assert.Equal("a", vm.SelectedItem!.Content);
        vm.MoveFirst();
        Assert.Equal("c", vm.SelectedItem!.Content);
    }

    [Fact]
    public void JumpToSlot_MapsOneBased_ZeroIsTenth()
    {
        for (var i = 1; i <= 10; i++)
        {
            Save("item" + i, minute: i);
        }
        var vm = Subject(); // [item10 .. item1]

        vm.JumpToSlot(1);
        Assert.Equal("item10", vm.SelectedItem!.Content);
        vm.JumpToSlot(0);
        Assert.Equal("item1", vm.SelectedItem!.Content);
    }

    [Fact]
    public void JumpToSlot_OutOfRange_KeepsSelection()
    {
        Save("a", minute: 0);
        var vm = Subject();

        vm.JumpToSlot(5);

        Assert.Equal("a", vm.SelectedItem!.Content);
    }

    [Fact]
    public void Search_FiltersLive_ResetsSelection()
    {
        Save("hello world", minute: 0);
        Save("other", minute: 1);
        var vm = Subject();
        vm.MoveLast();

        vm.SetSearch("hello");

        Assert.Equal(["hello world"], vm.VisibleItems.Select(i => i.Content));
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void Search_EmptyFilter_SelectsNothing()
    {
        Save("hello", minute: 0);
        var vm = Subject();

        vm.SetSearch("zzz");

        Assert.Empty(vm.VisibleItems);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.Null(vm.SelectedItem);
        Assert.False(vm.TogglePinSelected());
        Assert.False(vm.DeleteSelected(force: true));
    }

    [Fact]
    public void PinsFilter_ShowsOnlyPinned()
    {
        Save("plain", minute: 0);
        Save("fav", minute: 1, pinned: true);
        var vm = Subject();

        vm.TogglePinsFilter();

        Assert.True(vm.PinsOnly);
        Assert.Equal(["fav"], vm.VisibleItems.Select(i => i.Content));
        vm.TogglePinsFilter();
        Assert.False(vm.PinsOnly);
        Assert.Equal(2, vm.VisibleItems.Count);
    }

    [Fact]
    public void TypeCycle_RunsAllKindsThenAll()
    {
        Save("t", ItemKind.Text, minute: 0);
        Save("c", ItemKind.Code, minute: 1);
        var vm = Subject();

        vm.CycleTypeNext();
        Assert.Equal(ItemKind.Text, vm.TypeFilter);
        Assert.Equal(["t"], vm.VisibleItems.Select(i => i.Content));

        // Walk to the end of the order and back to unfiltered.
        for (var i = 0; i < 8; i++)
        {
            vm.CycleTypeNext();
        }
        Assert.Null(vm.TypeFilter);
        Assert.Equal(2, vm.VisibleItems.Count);
    }

    [Fact]
    public void TypeCyclePrevious_StartsFromLast()
    {
        var vm = Subject();

        vm.CycleTypePrevious();

        Assert.Equal(ItemKind.Color, vm.TypeFilter);
    }

    [Fact]
    public void TagCycle_RunsAllNineThenAll()
    {
        var vm = Subject();

        vm.CycleTagNext();
        Assert.Equal(ItemTags.All[0], vm.TagFilter);
        for (var i = 0; i < 9; i++)
        {
            vm.CycleTagNext();
        }
        Assert.Null(vm.TagFilter);

        vm.CycleTagPrevious();
        Assert.Equal(ItemTags.All[^1], vm.TagFilter);
    }

    [Fact]
    public void TogglePinSelected_FlipsAndRefilters()
    {
        Save("a", minute: 0);
        var vm = Subject();
        Assert.False(vm.VisibleItems.Single().Pinned);

        vm.TogglePinSelected();
        Assert.True(vm.VisibleItems.Single().Pinned);

        vm.TogglePinsFilter();
        Assert.Equal(["a"], vm.VisibleItems.Select(i => i.Content));

        vm.TogglePinSelected();
        Assert.Empty(vm.VisibleItems);
    }

    [Fact]
    public void DeleteSelected_RemovesUnpinned()
    {
        Save("a", minute: 0);
        Save("b", minute: 1);
        var vm = Subject();

        Assert.True(vm.DeleteSelected(force: false));
        Assert.Equal(["a"], vm.VisibleItems.Select(i => i.Content));
        Assert.NotNull(vm.SelectedItem);
    }

    [Fact]
    public void DeleteSelected_RefusesPinnedUnlessForced()
    {
        Save("fav", minute: 0, pinned: true);
        var vm = Subject();

        Assert.False(vm.DeleteSelected(force: false));
        Assert.Single(vm.VisibleItems);
        Assert.True(vm.DeleteSelected(force: true));
        Assert.Empty(vm.VisibleItems);
        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public void ClearKeepProtected_RemovesOnlyUnprotected()
    {
        Save("plain", minute: 0);
        Save("fav", minute: 1, pinned: true);
        Save("tagged", minute: 2, tag: ItemTags.All[0]);
        var vm = Subject();

        Assert.Equal(1, vm.ClearKeepProtected());
        Assert.Equal(2, vm.VisibleItems.Count);
    }

    [Fact]
    public void Visibility_HidesOnDeactivatedAndEscape()
    {
        var vm = Subject();
        Assert.False(vm.IsVisible);

        vm.Show(incognito: false);
        Assert.True(vm.IsVisible);
        vm.OnDeactivated();
        Assert.False(vm.IsVisible);

        vm.Toggle();
        Assert.True(vm.IsVisible);
        vm.OnEscape();
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void Show_SetsIncognito_TogglePreservesIt()
    {
        var vm = Subject();

        vm.Show(incognito: true);

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsIncognito);
        vm.Toggle(); // hide
        vm.Toggle(); // show again
        Assert.True(vm.IsIncognito);
    }

    [Fact]
    public void Incognito_Toggles()
    {
        var vm = Subject();

        vm.ToggleIncognito();
        Assert.True(vm.IsIncognito);
        vm.SetIncognito(false);
        Assert.False(vm.IsIncognito);
    }

    [Fact]
    public void Theme_DefaultsDark_ProfileDefaultsDefault()
    {
        var vm = Subject();

        Assert.Equal(PopupTheme.Dark, vm.Theme);
        Assert.Equal(PopupProfile.Default, vm.Profile);
        vm.SetTheme(PopupTheme.HighContrast);
        vm.SetProfile(PopupProfile.Compact);
        Assert.Equal(PopupTheme.HighContrast, vm.Theme);
        Assert.Equal(PopupProfile.Compact, vm.Profile);
    }
}
