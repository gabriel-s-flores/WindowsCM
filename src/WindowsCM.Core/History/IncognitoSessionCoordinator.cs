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

    public ClipboardItem AddOrUpdate(ClipboardItem item) => ActiveStore.AddOrUpdate(item);

    public IReadOnlyList<ClipboardItem> List() => ActiveStore.List();

    public long TryUpdateContent(long id, ItemKind kind, string content) =>
        ActiveStore.TryUpdateContent(id, kind, content);

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true) =>
        ActiveStore.Clear(keepProtected, protectPinned, protectTagged);

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true) =>
        ActiveStore.Evict(maxCount, maxAgeMinutes, utcNow, protectPinned, protectTagged);

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false) =>
        ActiveStore.Search(query, pinned, tag, kind, excludePinned, excludeTagged);

    public void RefreshDate(long id, DateTime utcNow) => ActiveStore.RefreshDate(id, utcNow);

    public bool Delete(long id) => ActiveStore.Delete(id);

    public void SetPinned(long id, bool pinned) => ActiveStore.SetPinned(id, pinned);

    public void SetTag(long id, string? tag) => ActiveStore.SetTag(id, tag);

    public void SetTitle(long id, string? title) => ActiveStore.SetTitle(id, title);

    public void SetMetadata(long id, string? metadataJson) => ActiveStore.SetMetadata(id, metadataJson);

    public void SetMetadataAndTitle(long id, string? metadataJson, string? title) =>
        ActiveStore.SetMetadataAndTitle(id, metadataJson, title);

    #endregion


    #region IImageAssetStore Forwarding

    public string Directory => ActiveImages.Directory;

    public string SaveIfAbsent(string fileName, byte[] bytes) => ActiveImages.SaveIfAbsent(fileName, bytes);

    public void SweepOrphans(IEnumerable<string> referencedFileNames) => ActiveImages.SweepOrphans(referencedFileNames);

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
