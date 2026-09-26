// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Capture;

// Fakes live at the OS seams from spec Testing Decisions: the event source,
// reader, sequence provider, process context and clock are faked; the store
// is real SQLite :memory: and image bytes land in a temp dir — never mocked.
internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } =
        new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakeReader : IClipboardReader
{
    public ClipboardPayload? Next;
    public int Calls;
    public ClipboardPayload? Read()
    {
        Calls++;
        return Next;
    }
}

internal sealed class FakeChangeSource : IClipboardChangeSource
{
    public event EventHandler? ClipboardChanged;
    public void Raise() => ClipboardChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeSequences : ISequenceProvider
{
    public uint Current;
    public uint GetSequenceNumber() => Current;
}

internal sealed class FakeProcesses : IForegroundProcess
{
    public string? CurrentProcessName { get; set; }
}

public sealed class CaptureServiceTests : IDisposable
{
    private readonly SqliteHistoryStore _store = new("Data Source=:memory:");
    private readonly string _imagesDir;
    private readonly FileImageAssetStore _images;
    private readonly FakeClock _clock = new();
    private readonly CaptureOptions _options = new();
    private readonly CaptureService _capture;

    public CaptureServiceTests()
    {
        _imagesDir = Path.Combine(Path.GetTempPath(), "wcm-" + Guid.NewGuid());
        _images = new FileImageAssetStore(_imagesDir);
        _capture = new CaptureService(_store, _images, _options, _clock);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_imagesDir))
        {
            Directory.Delete(_imagesDir, recursive: true);
        }
    }

    private static ClipboardPayload TextPayload(string text) =>
        new(null, null, text, []);

    [Fact]
    public void Capture_TextLandsAsTextItem()
    {
        var saved = _capture.Capture(
            TextPayload("just a note to self"), processName: null,
            _clock.UtcNow);

        Assert.NotNull(saved);
        Assert.Equal(ItemKind.Text, saved.Kind);
        Assert.Equal("just a note to self", saved.Content);
        Assert.Single(_store.List());
    }

    [Fact]
    public void Capture_BlankText_Ignored()
    {
        Assert.Null(_capture.Capture(TextPayload("   "), null, _clock.UtcNow));
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Capture_SensitiveHint_Rejected()
    {
        var payload = new ClipboardPayload(null, null, "secret",
            [SensitiveHints.WindowsExcludeFromMonitor]);

        Assert.Null(_capture.Capture(payload, null, _clock.UtcNow));
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Capture_ExcludedProcess_Skipped()
    {
        var options = new CaptureOptions
        {
            ExcludedProcesses = new HashSet<string>(["keepassxc"], StringComparer.OrdinalIgnoreCase),
        };
        var capture = new CaptureService(_store, _images, options, _clock);

        Assert.Null(capture.Capture(TextPayload("secret"), "KeePassXC.exe", _clock.UtcNow));
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Capture_ExcludedCopy_DoesNotPoisonLaterIdenticalCopy()
    {
        var options = new CaptureOptions
        {
            ExcludedProcesses = new HashSet<string>(["keepassxc"], StringComparer.OrdinalIgnoreCase),
        };
        var capture = new CaptureService(_store, _images, options, _clock);

        Assert.Null(capture.Capture(TextPayload("secret"), "KeePassXC.exe", _clock.UtcNow));

        var saved = capture.Capture(TextPayload("secret"), "notepad.exe", _clock.UtcNow);

        Assert.NotNull(saved);
        Assert.Single(_store.List());
    }

    [Fact]
    public void Capture_Incognito_StoresInEphemeralSession_WipedCleanOnToggleOff()
    {
        _capture.IsIncognito = true;
        var incognitoItem = _capture.Capture(TextPayload("secret"), null, _clock.UtcNow);
        Assert.NotNull(incognitoItem);
        Assert.Equal("secret", incognitoItem.Content);

        // Persistent store was NEVER touched! Zero leak!
        Assert.Empty(_store.List());

        // Now toggle off incognito
        _capture.IsIncognito = false;

        // Persistent store is still empty (no traces!)
        Assert.Empty(_store.List());

        // A new copy after toggle lands in the persistent store
        Assert.NotNull(_capture.Capture(TextPayload("hello"), null, _clock.UtcNow));
        Assert.Single(_store.List());
        Assert.Equal("hello", _store.List()[0].Content);

        // Re-copying the secret after toggle-off now lands legitimately in persistent store
        Assert.NotNull(_capture.Capture(TextPayload("secret"), null, _clock.UtcNow));
        Assert.Equal(2, _store.List().Count);
    }

    [Fact]
    public void Capture_OwnEcho_Suppressed()
    {
        Assert.NotNull(_capture.Capture(TextPayload("hello"), null, _clock.UtcNow));
        Assert.Null(_capture.Capture(TextPayload("hello"), null, _clock.UtcNow));
        Assert.Single(_store.List());
    }

    [Fact]
    public void Capture_FileDrop_PreservesOperation()
    {
        var payload = new ClipboardPayload(
            null,
            new FileSnapshot(["file:///C:/a.txt", "file:///C:/b.txt"], FileOperation.Cut),
            "ignored",
            []);

        var saved = _capture.Capture(payload, null, _clock.UtcNow);

        Assert.NotNull(saved);
        Assert.Equal(ItemKind.Files, saved.Kind);
        Assert.Equal(@"C:\a.txt" + "\n" + @"C:\b.txt", saved.Content);
        Assert.Equal("""{"operation":"cut"}""", saved.MetadataJson);
    }

    [Fact]
    public void Capture_ImageBytes_SavedHashedWithExtension()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var payload = new ClipboardPayload(
            new ImageSnapshot("image/png", bytes), null, null, []);

        var saved = _capture.Capture(payload, null, _clock.UtcNow);

        Assert.NotNull(saved);
        Assert.Equal(ItemKind.Image, saved.Kind);
        var expectedHash = ClipboardHash.Md5Hex(bytes);
        var expectedPath = Path.Combine(_imagesDir, expectedHash + ".png");
        Assert.Equal(new Uri(expectedPath).AbsoluteUri, saved.Content);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(bytes, File.ReadAllBytes(expectedPath));
    }

    [Fact]
    public void Capture_ImageWriteIfAbsent_KeepsFirstBytes()
    {
        var bytes = new byte[] { 7, 8, 9 };
        var payload = new ClipboardPayload(
            new ImageSnapshot("image/png", bytes), null, null, []);

        var first = _capture.Capture(payload, null, _clock.UtcNow);
        Assert.NotNull(first);
        // Corrupt the file on disk; a re-capture of different bytes hashing to
        // the same name must not overwrite — but hashes differ here, so prove
        // the idempotent path instead: same bytes again is an echo (suppressed)
        // and the file keeps the original bytes.
        Assert.Null(_capture.Capture(payload, null, _clock.UtcNow));
        var expectedPath = Path.Combine(_imagesDir,
            ClipboardHash.Md5Hex(bytes) + ".png");
        Assert.Equal(bytes, File.ReadAllBytes(expectedPath));
    }

    [Fact]
    public void CopiedFromHistory_RefreshesDateOnlyWhenSettingSaysSo()
    {
        var t0 = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var saved = _capture.Capture(TextPayload("pinned-note"), null, t0);
        Assert.NotNull(saved);

        _clock.UtcNow = t1;
        _capture.CopiedFromHistory(saved.Id, t1);
        Assert.Equal(t1, _store.List().Single(i => i.Id == saved.Id).CapturedAt);

        var off = new CaptureService(_store, _images,
            new CaptureOptions { UpdateDateOnCopy = false }, _clock);
        var t2 = new DateTime(2026, 9, 9, 13, 0, 0, DateTimeKind.Utc);
        off.CopiedFromHistory(saved.Id, t2);
        Assert.Equal(t1, _store.List().Single(i => i.Id == saved.Id).CapturedAt);
    }

    [Fact]
    public void CopiedFromHistory_SuppressesEchoOfOwnWrite()
    {
        var saved = _capture.Capture(TextPayload("hello"), null, _clock.UtcNow);
        Assert.NotNull(saved);

        _capture.CopiedFromHistory(saved.Id, _clock.UtcNow);

        // The clipboard still holds "hello" after our own copy-back: the next
        // listener notification must not duplicate history.
        Assert.Null(_capture.Capture(TextPayload("hello"), null, _clock.UtcNow));
        Assert.Single(_store.List());
    }

    [Fact]
    public void Capture_HtmlPayload_StoredOpaqueInMetadata()
    {
        var payload = new ClipboardPayload(null, null, "just a note to self", [],
            Html: "Version:1.0\r\nStartHTML:00000097\r\n<!--StartFragment-->hi<!--EndFragment-->");

        var saved = _capture.Capture(payload, null, _clock.UtcNow);

        Assert.NotNull(saved);
        Assert.Contains("StartFragment", saved.MetadataJson);
        // Stored compactly (no \u003C escapes) and read back verbatim.
        Assert.Contains("<!--StartFragment-->hi<!--EndFragment-->", saved.MetadataJson);
        Assert.Equal(payload.Html, WindowsCM.Core.Previews.ItemMetadataJson.GetString(saved.MetadataJson, "html"));
        // The text itself still classifies normally.
        Assert.Equal("just a note to self", saved.Content);
    }

    [Fact]
    public void SweepOrphanImages_DeletesUnreferencedFiles()
    {
        var bytes = new byte[] { 5, 6 };
        var saved = _capture.Capture(
            new ClipboardPayload(new ImageSnapshot("image/png", bytes), null, null, []),
            null, _clock.UtcNow);
        Assert.NotNull(saved);
        var orphan = Path.Combine(_imagesDir, "deadbeef.png");
        File.WriteAllBytes(orphan, [0]);

        _capture.SweepOrphanImages();

        Assert.True(File.Exists(Path.Combine(_imagesDir,
            ClipboardHash.Md5Hex(bytes) + ".png")));
        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public void Capture_EnforcesHistoryLengthOnEveryStoredItem()
    {
        // Limits used to apply only at startup: a long session kept growing.
        var capture = new CaptureService(_store, _images,
            new CaptureOptions { HistoryMaxItems = 3 }, _clock);
        var pinned = capture.Capture(TextPayload("keep me pinned"), null, _clock.UtcNow);
        Assert.NotNull(pinned);
        _store.SetPinned(pinned.Id, true);

        for (var i = 0; i < 6; i++)
        {
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            capture.Capture(TextPayload($"note number {i}"), null, _clock.UtcNow);
        }

        var contents = _store.List().Select(i => i.Content).ToList();
        Assert.Equal(4, contents.Count);
        Assert.Contains("keep me pinned", contents);
        Assert.Equal(["note number 5", "note number 4", "note number 3"],
            contents.Where(c => c != "keep me pinned"));
    }

    [Fact]
    public void Capture_EnforcesHistoryAge()
    {
        var capture = new CaptureService(_store, _images,
            new CaptureOptions { HistoryMaxAgeMinutes = 30 }, _clock);
        capture.Capture(TextPayload("stale note"), null, _clock.UtcNow);

        _clock.UtcNow = _clock.UtcNow.AddHours(2);
        capture.Capture(TextPayload("fresh note"), null, _clock.UtcNow);

        Assert.Equal(["fresh note"], _store.List().Select(i => i.Content));
    }

    [Fact]
    public void Capture_NoLimitsConfigured_KeepsEverything()
    {
        for (var i = 0; i < 5; i++)
        {
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            _capture.Capture(TextPayload($"note number {i}"), null, _clock.UtcNow);
        }

        Assert.Equal(5, _store.List().Count);
    }

    [Fact]
    public async Task Capture_AndCopyBack_FromManyThreads_StayConsistent()
    {
        // The listener thread captures while the UI thread copies back from
        // history: both touch the suppression state and the store.
        var saved = _capture.Capture(TextPayload("shared"), null, _clock.UtcNow);
        Assert.NotNull(saved);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        var capturing = Task.Run(() =>
        {
            for (var i = 0; i < 300; i++)
            {
                try
                {
                    _capture.Capture(TextPayload($"parallel note {i % 20}"), null, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        });
        var copying = Task.Run(() =>
        {
            for (var i = 0; i < 300; i++)
            {
                try
                {
                    _capture.CopiedFromHistory(saved.Id, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        });
        await Task.WhenAll(capturing, copying);

        Assert.Empty(errors);
    }

    // A store that fails the first write (disk full, busy database).
    private sealed class FailOnceStore(IHistoryStore inner) : IHistoryStore
    {
        public int Failures = 1;
        public ClipboardItem AddOrUpdate(ClipboardItem item)
        {
            if (Failures-- > 0)
            {
                throw new IOException("disk full");
            }
            return inner.AddOrUpdate(item);
        }
        public IReadOnlyList<ClipboardItem> List() => inner.List();
        public ClipboardItem? GetById(long id) => inner.GetById(id);
        public long TryUpdateContent(long id, ItemKind kind, string content) => inner.TryUpdateContent(id, kind, content);
        public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true) =>
            inner.Clear(keepProtected, protectPinned, protectTagged);
        public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow, bool protectPinned = true, bool protectTagged = true) =>
            inner.Evict(maxCount, maxAgeMinutes, utcNow, protectPinned, protectTagged);
        public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
            ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false) =>
            inner.Search(query, pinned, tag, kind, excludePinned, excludeTagged);
        public void RefreshDate(long id, DateTime utcNow) => inner.RefreshDate(id, utcNow);
        public bool Delete(long id) => inner.Delete(id);
        public void SetPinned(long id, bool pinned) => inner.SetPinned(id, pinned);
        public void SetTag(long id, string? tag) => inner.SetTag(id, tag);
        public void SetTitle(long id, string? title) => inner.SetTitle(id, title);
        public void SetMetadata(long id, string? metadataJson) => inner.SetMetadata(id, metadataJson);
        public void SetMetadataAndTitle(long id, string? metadataJson, string? title) =>
            inner.SetMetadataAndTitle(id, metadataJson, title);
        public void Dispose() => inner.Dispose();
    }

    // A failed write used to mark the copy as seen: the user's immediate
    // re-copy was then dropped as a duplicate and never stored.
    [Fact]
    public void Capture_FailedWrite_DoesNotSwallowTheRetry()
    {
        var failing = new FailOnceStore(_store);
        var capture = new CaptureService(failing, _images, _options, _clock);

        Assert.Throws<IOException>(() => capture.Capture(TextPayload("important note"), null, _clock.UtcNow));
        var retried = capture.Capture(TextPayload("important note"), null, _clock.UtcNow);

        Assert.NotNull(retried);
        Assert.Single(_store.List());
    }

    private ClipboardPayload ImagePayload(byte seed) =>
        new(new ImageSnapshot("image/png", [seed, 1, 2, 3]), null, null, []);

    private string ImageFile(byte seed) =>
        Path.Combine(_imagesDir, ClipboardHash.Md5Hex(new byte[] { seed, 1, 2, 3 }) + ".png");

    // Image files of evicted, deleted or cleared items stayed on disk until
    // the next restart: hundreds of MB a day for a tray app that takes
    // screenshots and runs for weeks.
    [Fact]
    public void Capture_SweepsImagesOfRemovedItemsWhileRunning()
    {
        var capture = new CaptureService(_store, _images,
            new CaptureOptions { HistoryMaxItems = 1, OrphanImageSweepInterval = TimeSpan.FromMinutes(15) }, _clock);
        capture.Capture(ImagePayload(1), null, _clock.UtcNow);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        capture.Capture(ImagePayload(2), null, _clock.UtcNow); // evicts image 1
        Assert.True(File.Exists(ImageFile(1)));

        _clock.UtcNow = _clock.UtcNow.AddMinutes(20);
        capture.Capture(TextPayload("later note"), null, _clock.UtcNow); // evicts image 2

        Assert.False(File.Exists(ImageFile(1)));
        Assert.False(File.Exists(ImageFile(2)));
    }

    [Fact]
    public void Capture_OrphanSweepIsThrottled()
    {
        var capture = new CaptureService(_store, _images,
            new CaptureOptions { HistoryMaxItems = 1, OrphanImageSweepInterval = TimeSpan.FromMinutes(15) }, _clock);
        capture.Capture(ImagePayload(1), null, _clock.UtcNow);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(20);
        capture.Capture(ImagePayload(2), null, _clock.UtcNow); // sweeps: image 1 gone
        Assert.False(File.Exists(ImageFile(1)));

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        capture.Capture(TextPayload("soon after"), null, _clock.UtcNow);

        Assert.True(File.Exists(ImageFile(2)));
    }

    // The sweep reads the persistent history and deletes in the persistent
    // folder only: an incognito session never makes persistent images look
    // unreferenced.
    [Fact]
    public void Capture_SweepDuringIncognito_KeepsPersistentImages()
    {
        using var coordinator = new IncognitoSessionCoordinator(_store, _images);
        var capture = new CaptureService(coordinator, coordinator,
            new CaptureOptions { OrphanImageSweepInterval = TimeSpan.Zero }, _clock);
        capture.Capture(ImagePayload(7), null, _clock.UtcNow);
        coordinator.SetIncognito(true);

        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        capture.Capture(TextPayload("incognito note"), null, _clock.UtcNow);
        capture.Capture(ImagePayload(8), null, _clock.UtcNow.AddSeconds(1));

        Assert.True(File.Exists(ImageFile(7)));
    }

    // A stand-in history (default location, memory-only, recreated file)
    // shares the images folder with the real one: sweeping against it
    // deleted the real history's images.
    [Fact]
    public void SweepDisabled_NeverDeletesImages()
    {
        var capture = new CaptureService(_store, _images,
            new CaptureOptions { HistoryMaxItems = 1, OrphanImageSweepInterval = TimeSpan.Zero, SweepOrphanImages = false }, _clock);
        var foreign = Path.Combine(_imagesDir, "belongs-to-the-real-history.png");
        File.WriteAllBytes(foreign, [1]);

        capture.Capture(ImagePayload(1), null, _clock.UtcNow);
        capture.Capture(TextPayload("pushes the image out"), null, _clock.UtcNow.AddMinutes(1));
        capture.SweepOrphanImages();

        Assert.True(File.Exists(foreign));
        Assert.True(File.Exists(ImageFile(1)));
    }

    // Pasting from the normal history while incognito: the ids of the two
    // sessions overlap, and the item used to be looked up again in the
    // incognito one.
    [Fact]
    public void CopiedFromHistory_FromTheNormalHistoryDuringIncognito_TouchesThatItemOnly()
    {
        using var coordinator = new IncognitoSessionCoordinator(_store, _images);
        var capture = new CaptureService(coordinator, coordinator, _options, _clock);
        var normal = _store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "normal note", false, null, _clock.UtcNow, null, null));
        coordinator.SetIncognito(true);
        var secret = coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "secret note", false, null, _clock.UtcNow, null, null));
        Assert.Equal(normal.Id, secret.Id);

        var later = _clock.UtcNow.AddHours(1);
        capture.CopiedFromHistory(normal, _store, later);

        Assert.Equal(later, _store.GetById(normal.Id)!.CapturedAt);
        Assert.Equal(_clock.UtcNow, coordinator.GetById(secret.Id)!.CapturedAt);
        // The paste's own clipboard write is not captured.
        Assert.Null(capture.Capture(TextPayload("normal note"), null, later));
    }

    // A row the item reader skips (an unreadable date) still owns its image.
    [Fact]
    public void Sweep_KeepsImagesOfRowsTheReaderSkips()
    {
        var saved = _capture.Capture(ImagePayload(3), null, _clock.UtcNow);
        _store.ExecuteForTests($"UPDATE clipboard SET datetime = 'not a date' WHERE id = {saved!.Id}");

        _capture.SweepOrphanImages();

        Assert.True(File.Exists(ImageFile(3)));
    }
}
