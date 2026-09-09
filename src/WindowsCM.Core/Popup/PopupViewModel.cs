// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Popup;

// Popup state machine (spec Testing Decisions): selection, live filter,
// pins filter, type/tag cycles, pin/delete/clear and incognito/theme over
// any IHistoryStore. Production passes the real SQLite store; tests pass a
// fake. No UI automation in v1 — the WPF window is a thin renderer over
// this plus PopupKeyboardMap (global chords in InputBindings, list chords
// in PreviewKeyDown skipping the search box) and PopupPlacement.
//
// Selection wraps on Next/Previous (prototype MoveSelection parity);
// First/Last clamp; Jump out of range keeps the current selection.
public sealed class PopupViewModel : ITrayPopup
{
    private static readonly ItemKind[] TypeOrder =
    [
        ItemKind.Text,
        ItemKind.Code,
        ItemKind.Image,
        ItemKind.File,
        ItemKind.Files,
        ItemKind.Link,
        ItemKind.Character,
        ItemKind.Color,
    ];

    private readonly IHistoryStore _store;

    public PopupViewModel(IHistoryStore store)
    {
        _store = store;
        Refresh();
    }

    public bool IsVisible { get; private set; }
    public bool IsIncognito { get; private set; }
    public string SearchText { get; private set; } = "";
    public bool PinsOnly { get; private set; }
    public ItemKind? TypeFilter { get; private set; }
    public string? TagFilter { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public IReadOnlyList<ClipboardItem> VisibleItems { get; private set; } = [];
    public PopupTheme Theme { get; private set; } = PopupCards.DefaultTheme;
    public PopupProfile Profile { get; private set; } = PopupCards.FirstRunProfile;

    public ClipboardItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < VisibleItems.Count
            ? VisibleItems[SelectedIndex]
            : null;

    public void Show(bool incognito)
    {
        IsIncognito = incognito;
        IsVisible = true;
        Refresh();
    }

    public void Show() => Show(IsIncognito);

    public void Hide() => IsVisible = false;

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    // Focus loss never lingers (Window.Deactivated parity).
    public void OnDeactivated() => Hide();

    // Esc closes (PreviewKeyDown parity, even from the search box).
    public void OnEscape() => Hide();

    public void SetSearch(string text)
    {
        SearchText = text;
        SelectedIndex = 0;
        Refresh();
    }

    public void TogglePinsFilter()
    {
        PinsOnly = !PinsOnly;
        SelectedIndex = 0;
        Refresh();
    }

    public void CycleTypeNext()
    {
        TypeFilter = NextType(TypeFilter, forward: true);
        SelectedIndex = 0;
        Refresh();
    }

    public void CycleTypePrevious()
    {
        TypeFilter = NextType(TypeFilter, forward: false);
        SelectedIndex = 0;
        Refresh();
    }

    public void CycleTagNext()
    {
        TagFilter = NextTag(TagFilter, forward: true);
        SelectedIndex = 0;
        Refresh();
    }

    public void CycleTagPrevious()
    {
        TagFilter = NextTag(TagFilter, forward: false);
        SelectedIndex = 0;
        Refresh();
    }

    public void MoveNext()
    {
        if (VisibleItems.Count == 0)
        {
            return;
        }
        SelectedIndex = (SelectedIndex + 1) % VisibleItems.Count;
    }

    public void MovePrevious()
    {
        if (VisibleItems.Count == 0)
        {
            return;
        }
        SelectedIndex = (SelectedIndex - 1 + VisibleItems.Count) % VisibleItems.Count;
    }

    public void MoveFirst()
    {
        if (VisibleItems.Count == 0)
        {
            return;
        }
        SelectedIndex = 0;
    }

    public void MoveLast()
    {
        if (VisibleItems.Count == 0)
        {
            return;
        }
        SelectedIndex = VisibleItems.Count - 1;
    }

    // Ctrl+0..9 parity: slots 1..9 address rows 1..9, slot 0 the 10th.
    // Out of range keeps the current selection.
    public void JumpToSlot(int slot)
    {
        if (VisibleItems.Count == 0)
        {
            return;
        }
        var index = slot == 0 ? 9 : slot - 1;
        if (index < 0 || index >= VisibleItems.Count)
        {
            return;
        }
        SelectedIndex = index;
    }

    public bool TogglePinSelected()
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return false;
        }
        _store.SetPinned(selected.Id, !selected.Pinned);
        Refresh();
        return true;
    }

    // Plain Delete refuses pinned items; Shift+Delete forces (research 03
    // §5 "Delete (＋Shift força)" parity). False when nothing happened.
    public bool DeleteSelected(bool force)
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return false;
        }
        if (selected.Pinned && !force)
        {
            return false;
        }
        var removed = _store.Delete(selected.Id);
        Refresh();
        return removed;
    }

    public int ClearKeepProtected()
    {
        var removed = _store.Clear(keepProtected: true);
        SelectedIndex = 0;
        Refresh();
        return removed;
    }

    public int ClearAll()
    {
        var removed = _store.Clear(keepProtected: false);
        SelectedIndex = 0;
        Refresh();
        return removed;
    }

    public void SetIncognito(bool on) => IsIncognito = on;

    public void ToggleIncognito() => IsIncognito = !IsIncognito;

    public void SetTheme(PopupTheme theme) => Theme = theme;

    public void SetProfile(PopupProfile profile) => Profile = profile;

    public void Refresh()
    {
        VisibleItems = _store.Search(
            SearchText,
            pinned: PinsOnly ? true : null,
            tag: TagFilter,
            kind: TypeFilter);
        if (VisibleItems.Count == 0)
        {
            SelectedIndex = -1;
        }
        else if (SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }
        else if (SelectedIndex >= VisibleItems.Count)
        {
            SelectedIndex = VisibleItems.Count - 1;
        }
    }

    private static ItemKind? NextType(ItemKind? current, bool forward)
    {
        if (current is null)
        {
            return forward ? TypeOrder[0] : TypeOrder[^1];
        }
        var at = Array.IndexOf(TypeOrder, current.Value);
        var next = at + (forward ? 1 : -1);
        if (next < 0 || next >= TypeOrder.Length)
        {
            return null;
        }
        return TypeOrder[next];
    }

    private static string? NextTag(string? current, bool forward)
    {
        var tags = ItemTags.All;
        if (current is null)
        {
            return forward ? tags[0] : tags[^1];
        }
        var at = -1;
        for (var i = 0; i < tags.Count; i++)
        {
            if (tags[i] == current)
            {
                at = i;
                break;
            }
        }
        if (at < 0)
        {
            return forward ? tags[0] : tags[^1];
        }
        var next = at + (forward ? 1 : -1);
        if (next < 0 || next >= tags.Count)
        {
            return null;
        }
        return tags[next];
    }
}
