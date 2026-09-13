// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.History;
using Xunit;

namespace WindowsCM.Core.Tests.History;

public sealed class IncognitoSessionCoordinatorTests : IDisposable
{
    private readonly SqliteHistoryStore _persistentStore;
    private readonly FileImageAssetStore _persistentImages;
    private readonly string _imagesDir;
    private readonly IncognitoSessionCoordinator _coordinator;

    public IncognitoSessionCoordinatorTests()
    {
        _persistentStore = new SqliteHistoryStore("Data Source=:memory:");
        _imagesDir = Path.Combine(Path.GetTempPath(), "WindowsCM_Test_Images_" + Guid.NewGuid().ToString("N"));
        _persistentImages = new FileImageAssetStore(_imagesDir);
        _coordinator = new IncognitoSessionCoordinator(_persistentStore, _persistentImages);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        _persistentStore.Dispose();
        if (Directory.Exists(_imagesDir))
        {
            try { Directory.Delete(_imagesDir, true); } catch { }
        }
    }

    [Fact]
    public void InitialState_IsNonIncognito_RoutesToPersistent()
    {
        Assert.False(_coordinator.IsIncognito);
        Assert.Same(_persistentStore, _coordinator.ActiveStore);
        Assert.Same(_persistentImages, _coordinator.ActiveImages);
    }

    [Fact]
    public void SetIncognitoTrue_ActivatesEphemeralStoreAndImages()
    {
        var changes = new List<bool>();
        _coordinator.IncognitoChanged += (_, active) => changes.Add(active);

        _coordinator.SetIncognito(true);

        Assert.True(_coordinator.IsIncognito);
        Assert.NotSame(_persistentStore, _coordinator.ActiveStore);
        Assert.NotSame(_persistentImages, _coordinator.ActiveImages);
        Assert.Equal([true], changes);
    }

    [Fact]
    public void SetIncognitoTrue_Idempotent()
    {
        _coordinator.SetIncognito(true);
        var store1 = _coordinator.ActiveStore;

        _coordinator.SetIncognito(true);
        Assert.Same(store1, _coordinator.ActiveStore);
    }

    [Fact]
    public void Incognito_StoresItemsInEphemeral_DoesNotLeakToPersistent()
    {
        // Normal item in persistent store
        var normal = new ClipboardItem(ItemKind.Text, "normal text", false, null, DateTime.UtcNow, null, null);
        _coordinator.AddOrUpdate(normal);
        Assert.Single(_persistentStore.List());

        // Turn on incognito
        _coordinator.SetIncognito(true);

        // Add incognito item
        var secret = new ClipboardItem(ItemKind.Text, "secret token", false, null, DateTime.UtcNow, null, null);
        var savedSecret = _coordinator.AddOrUpdate(secret);
        Assert.NotNull(savedSecret);

        // Active store sees both or only secret? Active store is incognito store, so it sees secret!
        Assert.Single(_coordinator.List());
        Assert.Equal("secret token", _coordinator.List()[0].Content);

        // Persistent store still only has "normal text" (zero leak!)
        Assert.Single(_persistentStore.List());
        Assert.Equal("normal text", _persistentStore.List()[0].Content);
    }

    [Fact]
    public void SetIncognitoFalse_DestroysEphemeralData_RevertsToPersistent()
    {
        var normal = new ClipboardItem(ItemKind.Text, "normal text", false, null, DateTime.UtcNow, null, null);
        _coordinator.AddOrUpdate(normal);

        _coordinator.SetIncognito(true);
        var secret = new ClipboardItem(ItemKind.Text, "secret token", false, null, DateTime.UtcNow, null, null);
        _coordinator.AddOrUpdate(secret);
        Assert.Equal("secret token", _coordinator.List()[0].Content);

        // Deactivate incognito
        _coordinator.SetIncognito(false);

        Assert.False(_coordinator.IsIncognito);
        Assert.Same(_persistentStore, _coordinator.ActiveStore);

        // Secret is completely wiped out!
        Assert.Single(_coordinator.List());
        Assert.Equal("normal text", _coordinator.List()[0].Content);
        Assert.DoesNotContain(_coordinator.List(), i => i.Content == "secret token");
    }

    [Fact]
    public void IncognitoImages_WipedFromDiskOnExit()
    {
        _coordinator.SetIncognito(true);

        var fakeBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var uri = _coordinator.SaveIfAbsent("secret.png", fakeBytes);
        Assert.StartsWith("file://", uri);

        var localPath = new Uri(uri).LocalPath;
        Assert.True(File.Exists(localPath));
        var tempIncognitoDir = Path.GetDirectoryName(localPath)!;
        Assert.True(Directory.Exists(tempIncognitoDir));

        // Deactivate incognito: directory and file must be completely purged from disk
        _coordinator.SetIncognito(false);

        Assert.False(File.Exists(localPath));
        Assert.False(Directory.Exists(tempIncognitoDir));
    }

    [Fact]
    public void ToggleIncognito_FlipsState()
    {
        Assert.False(_coordinator.IsIncognito);

        _coordinator.ToggleIncognito();
        Assert.True(_coordinator.IsIncognito);

        _coordinator.ToggleIncognito();
        Assert.False(_coordinator.IsIncognito);
    }

    [Fact]
    public void Search_InIncognito_FiltersOnlyIncognitoItems()
    {
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "persistent alpha", false, null, DateTime.UtcNow, null, null));

        _coordinator.SetIncognito(true);
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "incognito alpha", false, null, DateTime.UtcNow, null, null));
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "incognito beta", false, null, DateTime.UtcNow, null, null));

        var results = _coordinator.Search("alpha");
        Assert.Single(results);
        Assert.Equal("incognito alpha", results[0].Content);

        _coordinator.SetIncognito(false);
        var normalResults = _coordinator.Search("alpha");
        Assert.Single(normalResults);
        Assert.Equal("persistent alpha", normalResults[0].Content);
    }

    [Fact]
    public void Clear_InIncognito_ClearsOnlyIncognitoStore()
    {
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "persistent item", false, null, DateTime.UtcNow, null, null));

        _coordinator.SetIncognito(true);
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "temp item 1", false, null, DateTime.UtcNow, null, null));
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "temp item 2", false, null, DateTime.UtcNow, null, null));

        var removed = _coordinator.Clear(keepProtected: false);
        Assert.Equal(2, removed);
        Assert.Empty(_coordinator.List());

        _coordinator.SetIncognito(false);
        Assert.Single(_coordinator.List());
        Assert.Equal("persistent item", _coordinator.List()[0].Content);
    }

    [Fact]
    public void PersistentStore_ExposesPersistentItems_EvenWhenIncognitoActive()
    {
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "persistent alpha", false, null, DateTime.UtcNow, null, null));
        _coordinator.SetIncognito(true);
        _coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "incognito alpha", false, null, DateTime.UtcNow, null, null));

        Assert.Single(_coordinator.List());
        Assert.Equal("incognito alpha", _coordinator.List()[0].Content);

        Assert.Single(_coordinator.PersistentStore.List());
        Assert.Equal("persistent alpha", _coordinator.PersistentStore.List()[0].Content);
    }
}

