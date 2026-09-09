// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.History;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

// OS edges faked (spec Testing Decisions): writer, injector, foreground,
// elevation, image bytes and delay are fakes; the store is real SQLite
// :memory: and CaptureService is real (date policy + echo suppression).
internal sealed class FakeWriter : IClipboardWriter
{
    public List<string> Events = [];
    public Action<string>? Log;
    public ClipboardContents? Last;
    public Exception? Throw;
    public void Write(ClipboardContents contents)
    {
        Events.Add("write");
        Log?.Invoke("write");
        Last = contents;
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}

internal sealed class FakeInjector : IPasteInjector
{
    public List<string> Events = [];
    public Action<string>? Log;
    public PasteSequence? Last;
    public Exception? Throw;
    public void Inject(PasteSequence sequence)
    {
        Events.Add("inject");
        Log?.Invoke("inject");
        Last = sequence;
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}

internal sealed class FakeForeground : IForegroundWindow
{
    public IntPtr Current = new(123);
    public IntPtr GetCurrent() => Current;
}

internal sealed class FakeElevation : IElevationProbe
{
    public bool SelfElevated;
    public bool TargetElevated;
    public bool IsCurrentProcessElevated() => SelfElevated;
    public bool IsTargetElevated(IntPtr hwnd) => TargetElevated;
}

internal sealed class FakeImages : IImageFileReader
{
    public Dictionary<string, byte[]?> Files = new();
    public byte[]? LoadPng(string content) =>
        Files.TryGetValue(content, out var bytes) ? bytes : null;
}

internal sealed class FakeDelay : IPasteDelay
{
    public List<int> Asked = [];
    public Task Delay(int milliseconds, CancellationToken ct = default)
    {
        Asked.Add(milliseconds);
        return Task.CompletedTask;
    }
}

internal sealed class FakePasteClock : IClock
{
    public DateTime UtcNow { get; set; } =
        new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class PasteOrchestratorTests : IDisposable
{
    private readonly SqliteHistoryStore _store = new("Data Source=:memory:");
    private readonly FakeWriter _writer = new();
    private readonly FakeInjector _injector = new();
    private readonly FakeForeground _foreground = new();
    private readonly FakeElevation _elevation = new();
    private readonly FakeImages _images = new();
    private readonly FakeDelay _delay = new();
    private readonly FakePasteClock _clock = new();
    private readonly PasteOptions _options = new();
    private readonly CaptureService _capture;
    private readonly List<string> _order = [];
    private int _hides;

    private readonly string _imagesDir =
        Path.Combine(Path.GetTempPath(), "wcm-" + Guid.NewGuid());

    public PasteOrchestratorTests()
    {
        _writer.Log = _order.Add;
        _injector.Log = _order.Add;
        _capture = new CaptureService(
            _store,
            new FileImageAssetStore(_imagesDir),
            new CaptureOptions(),
            _clock);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_imagesDir))
        {
            Directory.Delete(_imagesDir, recursive: true);
        }
    }

    private PasteOrchestrator Subject() => new(
        _store, _capture, _images, _writer, _foreground, _elevation,
        _injector, _delay, _options, _clock);

    private ClipboardItem SaveText(string text = "hello")
    {
        var t0 = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
        return _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Text, text, false, null, t0, null, null));
    }

    private Task Hide()
    {
        _hides++;
        _order.Add("hide");
        return Task.CompletedTask;
    }



    [Fact]
    public async Task PlainEnter_CopiesBackAndPastesCtrlV()
    {
        var saved = SaveText();
        var target = new IntPtr(123);
        _foreground.Current = target;
        _clock.UtcNow = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        var outcome = await Subject().ExecuteAsync(saved.Id, target, shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Pasted, outcome.Status);
        Assert.Equal(PasteSequence.CtrlV, outcome.InjectedChord);
        Assert.Equal("hello", _writer.Last?.Text);
        Assert.Equal(PasteSequence.CtrlV, _injector.Last);
        Assert.Equal([200], _delay.Asked);
        // UI hides before injection so the target regains focus.
        Assert.Equal(["write", "hide", "inject"], _order);
        // Copy-from-history refreshes the date (update-date-on-copy default).
        Assert.Equal(_clock.UtcNow, _store.List().Single(i => i.Id == saved.Id).CapturedAt);
    }

    [Fact]
    public async Task ShiftEnter_CopiesWithoutPasting()
    {
        var saved = SaveText();

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: true, Hide);

        Assert.Equal(PasteStatus.CopiedOnly, outcome.Status);
        Assert.Equal("hello", _writer.Last?.Text);
        Assert.Empty(_injector.Events);
        Assert.Equal(0, _hides);
        Assert.Empty(_delay.Asked);
    }

    [Fact]
    public async Task Swap_InvertsChordsEndToEnd()
    {
        _options.SwapCopyPaste = true;
        var saved = SaveText();

        var plain = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);
        var shifted = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: true, Hide);

        Assert.Equal(PasteStatus.CopiedOnly, plain.Status);
        Assert.Equal(PasteStatus.Pasted, shifted.Status);
    }

    [Fact]
    public async Task ShiftInsert_OptIn_InjectsLegacyChord()
    {
        _options.PasteSequence = PasteSequence.ShiftInsert;
        var saved = SaveText();

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Pasted, outcome.Status);
        Assert.Equal(PasteSequence.ShiftInsert, outcome.InjectedChord);
        Assert.Equal(PasteSequence.ShiftInsert, _injector.Last);
    }

    [Fact]
    public async Task ForegroundLost_CopiesOnlyWithDiagnostics()
    {
        var saved = SaveText();
        _foreground.Current = new IntPtr(999); // target moved on

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.CopiedOnlyForegroundLost, outcome.Status);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Diagnostics));
        Assert.Equal("hello", _writer.Last?.Text); // copy-back still happened
        Assert.Empty(_injector.Events);
    }

    [Fact]
    public async Task ElevatedTarget_ReportedNeverSilent()
    {
        var saved = SaveText();
        _elevation.TargetElevated = true;
        _elevation.SelfElevated = false;

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.CopiedOnlyElevated, outcome.Status);
        Assert.Contains("elevat", outcome.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_injector.Events);
    }

    [Fact]
    public async Task ElevatedSelf_PastesNormally()
    {
        var saved = SaveText();
        _elevation.TargetElevated = true;
        _elevation.SelfElevated = true;

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Pasted, outcome.Status);
        Assert.NotNull(_injector.Last);
    }

    [Fact]
    public async Task MissingItem_FailsWithDiagnostics()
    {
        var outcome = await Subject().ExecuteAsync(
            9999, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Failed, outcome.Status);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Diagnostics));
        Assert.Null(_writer.Last);
        Assert.Empty(_injector.Events);
        Assert.Equal(0, _hides);
    }

    [Fact]
    public async Task MissingImageFile_FailsWithDiagnostics()
    {
        var saved = _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Image, new Uri(@"C:\gone.png").AbsoluteUri,
            false, null, _clock.UtcNow, null, null));

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Failed, outcome.Status);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Diagnostics));
        Assert.Null(_writer.Last);
    }

    [Fact]
    public async Task WriterFailure_SurfacesDiagnostics()
    {
        var saved = SaveText();
        _writer.Throw = new ClipboardWriteException("OpenClipboard denied");

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Failed, outcome.Status);
        Assert.Contains("denied", outcome.Diagnostics);
        Assert.Empty(_injector.Events);
    }

    [Fact]
    public async Task InjectorFailure_SurfacesDiagnostics()
    {
        var saved = SaveText();
        _injector.Throw = new PasteInjectionException("SendInput refused");

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Failed, outcome.Status);
        Assert.Contains("refused", outcome.Diagnostics);
    }

    [Fact]
    public async Task FileItem_WritesDropPathsAndPastes()
    {
        var content = string.Join("\n",
            new Uri(@"C:\a.txt").AbsoluteUri,
            new Uri(@"C:\b.txt").AbsoluteUri);
        var saved = _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Files, content, false, null, _clock.UtcNow, """{"operation":"cut"}""", null));

        var outcome = await Subject().ExecuteAsync(
            saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        Assert.Equal(PasteStatus.Pasted, outcome.Status);
        Assert.Equal([@"C:\a.txt", @"C:\b.txt"], _writer.Last?.FileLocalPaths);
        Assert.Null(_writer.Last?.Text);
    }

    [Fact]
    public async Task Execute_SuppressesOwnEcho()
    {
        var saved = SaveText("echo-me");
        await Subject().ExecuteAsync(saved.Id, new IntPtr(123), shiftHeld: false, Hide);

        // Our own clipboard write must not duplicate history on next notify.
        var echo = _capture.Capture(
            new ClipboardPayload(null, null, "echo-me", []), null, _clock.UtcNow);

        Assert.Null(echo);
        Assert.Single(_store.List());
    }
}
