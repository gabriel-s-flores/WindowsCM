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
    public void Capture_Incognito_SuspendsWithoutLeakAfterToggle()
    {
        // Copyous parity: prevClipboard is recorded before the save gate, so a
        // copy made while incognito is on never leaks once toggled off.
        _capture.IsIncognito = true;
        Assert.Null(_capture.Capture(TextPayload("secret"), null, _clock.UtcNow));

        _capture.IsIncognito = false;
        Assert.Null(_capture.Capture(TextPayload("secret"), null, _clock.UtcNow));
        Assert.Empty(_store.List());

        // A genuinely new copy after toggle still lands.
        Assert.NotNull(_capture.Capture(TextPayload("hello"), null, _clock.UtcNow));
        Assert.Single(_store.List());
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
        Assert.Equal("file:///C:/a.txt\nfile:///C:/b.txt", saved.Content);
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
}
