// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.History;

// Thread-safe store: the single SqliteConnection is touched from the UI
// thread (popup, orchestrator) and the pipe-server thread (remote
// clear/toggle). One lock around every seam keeps the stories 36/41
// cross-thread use honest.
public sealed class LockedHistoryStore : IHistoryStore
{
    private readonly IHistoryStore _inner;
    private readonly object _gate = new();
    private bool _disposed;

    public LockedHistoryStore(IHistoryStore inner) => _inner = inner;

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        lock (_gate)
        {
            return _inner.AddOrUpdate(item);
        }
    }

    public IReadOnlyList<ClipboardItem> List()
    {
        lock (_gate)
        {
            return _inner.List();
        }
    }

    public ClipboardItem? GetById(long id)
    {
        lock (_gate)
        {
            return _inner.GetById(id);
        }
    }

    public ClipboardItem? GetLatest()
    {
        lock (_gate)
        {
            return _inner.GetLatest();
        }
    }

    public long TryUpdateContent(long id, ItemKind kind, string content)
    {
        lock (_gate)
        {
            return _inner.TryUpdateContent(id, kind, content);
        }
    }

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true)
    {
        lock (_gate)
        {
            return _inner.Clear(keepProtected, protectPinned, protectTagged);
        }
    }

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true)
    {
        lock (_gate)
        {
            return _inner.Evict(maxCount, maxAgeMinutes, utcNow, protectPinned, protectTagged);
        }
    }

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false)
    {
        lock (_gate)
        {
            return _inner.Search(query, pinned, tag, kind, excludePinned, excludeTagged);
        }
    }

    public void RefreshDate(long id, DateTime utcNow)
    {
        lock (_gate)
        {
            _inner.RefreshDate(id, utcNow);
        }
    }

    public bool Delete(long id)
    {
        lock (_gate)
        {
            return _inner.Delete(id);
        }
    }

    public void SetPinned(long id, bool pinned)
    {
        lock (_gate)
        {
            _inner.SetPinned(id, pinned);
        }
    }

    public void SetTag(long id, string? tag)
    {
        lock (_gate)
        {
            _inner.SetTag(id, tag);
        }
    }

    public void SetTitle(long id, string? title)
    {
        lock (_gate)
        {
            _inner.SetTitle(id, title);
        }
    }

    public void SetMetadata(long id, string? metadataJson)
    {
        lock (_gate)
        {
            _inner.SetMetadata(id, metadataJson);
        }
    }

    public void SetMetadataAndTitle(long id, string? metadataJson, string? title)
    {
        lock (_gate)
        {
            _inner.SetMetadataAndTitle(id, metadataJson, title);
        }
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        lock (_gate)
        {
            _inner.Dispose();
        }
    }
}
