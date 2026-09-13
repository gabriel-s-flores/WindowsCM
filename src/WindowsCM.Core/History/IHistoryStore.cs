// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.History;

// Public seam for history persistence. Tested against real SQLite
// (:memory: in tests, file in production) — never mocked.
public interface IHistoryStore : IDisposable
{
    // Insert, or bump the date when (kind, content) already exists.
    // Returns the stored item with its Id.
    ClipboardItem AddOrUpdate(ClipboardItem item);

    // All items, newest first.
    IReadOnlyList<ClipboardItem> List();

    // Edit identity. Returns -1 when applied, or the conflicting item id
    // (Gda parity) leaving the edited item untouched.
    long TryUpdateContent(long id, ItemKind kind, string content);

    // Delete items. When keepProtected, pinned/tagged items survive
    // (each side disabled by its flag). Returns the removed row count.
    int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true);

    // Enforce history-length (maxCount, newest kept) and history-time
    // (maxAgeMinutes, 0 disables the age branch). Protected items survive
    // unless their flag is off. Returns the removed row count.
    int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true);

    // Substring search over content with title fallback (both
    // case-insensitive), newest first. Empty query matches everything;
    // pinned/tag/kind narrow the result (null = no filter), the exclude
    // flags drop pinned/tagged rows (settings defaults).
    IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false);

    // Refresh the date (copy-from-history with update-date-on-copy on).
    void RefreshDate(long id, DateTime utcNow);

    // Delete one item (popup Delete-key parity). Returns true when a row
    // was removed.
    bool Delete(long id);

    // Field edits (never collide: no identity change).
    void SetPinned(long id, bool pinned);
    void SetTag(long id, string? tag);
    void SetTitle(long id, string? title);
    void SetMetadata(long id, string? metadataJson);
    void SetMetadataAndTitle(long id, string? metadataJson, string? title);
}

