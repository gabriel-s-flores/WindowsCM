// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.History;

// Coordinated storage session for normal vs. incognito clipboard modes.
// When incognito is active, all captures, queries, searches, mutations and image
// assets are routed to an in-memory ephemeral store and a temp image directory.
// When incognito is deactivated, the in-memory SQLite database and all temporary
// image assets are wiped completely, with zero traces left on disk or in memory.
public sealed class IncognitoSessionCoordinator : IHistoryStore, IImageAssetStore, IIncognitoToggle, IDisposable
{
    private readonly IHistoryStore _persistentStore;
    private readonly IImageAssetStore _persistentImages;
    private SqliteHistoryStore? _incognitoStore;
    private EphemeralImageAssetStore? _incognitoImages;
    private readonly object _lock = new();

    public event EventHandler<bool>? IncognitoChanged;

    public IncognitoSessionCoordinator(
        IHistoryStore persistentStore,
        IImageAssetStore persistentImages)
    {
        _persistentStore = persistentStore ?? throw new ArgumentNullException(nameof(persistentStore));
        _persistentImages = persistentImages ?? throw new ArgumentNullException(nameof(persistentImages));
    }

    public bool IsIncognito { get; private set; }

    public IHistoryStore PersistentStore => _persistentStore;

    public IHistoryStore? EphemeralStore
    {
        get
        {
            lock (_lock)
            {
                return _incognitoStore;
            }
        }
    }

    public IHistoryStore ActiveStore
    {
        get
        {
            lock (_lock)
            {
                return (IsIncognito && _incognitoStore is not null) ? _incognitoStore : _persistentStore;
            }
        }
    }

    public IImageAssetStore ActiveImages
    {
        get
        {
            lock (_lock)
            {
                return (IsIncognito && _incognitoImages is not null) ? _incognitoImages : _persistentImages;
            }
        }
    }

    public void SetIncognito(bool on)
    {
        bool changed = false;
        lock (_lock)
        {
            if (on && !IsIncognito)
            {
                _incognitoStore = new SqliteHistoryStore("Data Source=:memory:");
                _incognitoImages = new EphemeralImageAssetStore();
                IsIncognito = true;
                changed = true;
            }
            else if (!on && IsIncognito)
            {
                _incognitoStore?.Dispose();
                _incognitoStore = null;

                _incognitoImages?.Dispose();
                _incognitoImages = null;

                IsIncognito = false;
                changed = true;
            }
        }

        if (changed)
        {
            IncognitoChanged?.Invoke(this, on);
        }
    }

    public void ToggleIncognito() => SetIncognito(!IsIncognito);

    #region IHistoryStore Forwarding

    // Every forwarded call runs under the lock that swaps and disposes the
    // ephemeral session: its in-memory SqliteConnection is not thread-safe
    // (capture thread, UI, link previews and the pipe all reach it), and
    // SetIncognito(false) must never dispose it under an in-flight query.
    private T Locked<T>(Func<IHistoryStore, T> call)
    {
        lock (_lock)
        {
            return call((IsIncognito && _incognitoStore is not null) ? _incognitoStore : _persistentStore);
        }
    }

    private void Locked(Action<IHistoryStore> call) =>
        Locked<bool>(store =>
        {
            call(store);
            return true;
        });

    private T LockedImages<T>(Func<IImageAssetStore, T> call)
    {
        lock (_lock)
        {
            return call((IsIncognito && _incognitoImages is not null) ? _incognitoImages : _persistentImages);
        }
    }

    public ClipboardItem AddOrUpdate(ClipboardItem item) => Locked(s => s.AddOrUpdate(item));

    public IReadOnlyList<ClipboardItem> List() => Locked(s => s.List());

    public ClipboardItem? GetById(long id) => Locked(s => s.GetById(id));

    public ClipboardItem? GetLatest() => Locked(s => s.GetLatest());

    public long TryUpdateContent(long id, ItemKind kind, string content) =>
        Locked(s => s.TryUpdateContent(id, kind, content));

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true) =>
        Locked(s => s.Clear(keepProtected, protectPinned, protectTagged));

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true) =>
        Locked(s => s.Evict(maxCount, maxAgeMinutes, utcNow, protectPinned, protectTagged));

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false) =>
        Locked(s => s.Search(query, pinned, tag, kind, excludePinned, excludeTagged));

    public void RefreshDate(long id, DateTime utcNow) => Locked(s => s.RefreshDate(id, utcNow));

    public bool Delete(long id) => Locked(s => s.Delete(id));

    public void SetPinned(long id, bool pinned) => Locked(s => s.SetPinned(id, pinned));

    public void SetTag(long id, string? tag) => Locked(s => s.SetTag(id, tag));

    public void SetTitle(long id, string? title) => Locked(s => s.SetTitle(id, title));

    public void SetMetadata(long id, string? metadataJson) => Locked(s => s.SetMetadata(id, metadataJson));

    public void SetMetadataAndTitle(long id, string? metadataJson, string? title) =>
        Locked(s => s.SetMetadataAndTitle(id, metadataJson, title));

    #endregion


    #region IImageAssetStore Forwarding

    public string Directory => LockedImages(i => i.Directory);

    public string SaveIfAbsent(string fileName, byte[] bytes) =>
        LockedImages(i => i.SaveIfAbsent(fileName, bytes));

    public void SweepOrphans(IEnumerable<string> referencedFileNames) =>
        LockedImages(i =>
        {
            i.SweepOrphans(referencedFileNames);
            return true;
        });

    #endregion

    public void Dispose()
    {
        lock (_lock)
        {
            _incognitoStore?.Dispose();
            _incognitoStore = null;

            _incognitoImages?.Dispose();
            _incognitoImages = null;

            IsIncognito = false;
        }
    }
}
