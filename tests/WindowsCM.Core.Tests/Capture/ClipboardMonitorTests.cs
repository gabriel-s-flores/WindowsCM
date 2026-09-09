// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Capture;

public sealed class ClipboardMonitorTests : IDisposable
{
    private readonly SqliteHistoryStore _store = new("Data Source=:memory:");
    private readonly string _imagesDir;
    private readonly FakeClock _clock = new();
    private readonly FakeReader _reader = new();
    private readonly FakeChangeSource _source = new();
    private readonly FakeSequences _sequences = new();
    private readonly FakeProcesses _processes = new();

    public ClipboardMonitorTests()
    {
        _imagesDir = Path.Combine(Path.GetTempPath(), "wcm-mon-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_imagesDir))
        {
            Directory.Delete(_imagesDir, recursive: true);
        }
    }

    private ClipboardMonitor Build(CaptureService? capture = null)
    {
        capture ??= new CaptureService(
            _store, new FileImageAssetStore(_imagesDir), new CaptureOptions(), _clock);
        return new ClipboardMonitor(_source, _reader, capture, _sequences, _processes, _clock);
    }

    [Fact]
    public void Notification_ReadsOnceAndCaptures()
    {
        using var monitor = Build();
        _reader.Next = new ClipboardPayload(null, null, "just a note to self", []);

        _source.Raise();

        Assert.Equal(1, _reader.Calls);
        Assert.Single(_store.List());
    }

    [Fact]
    public void NoNotification_NoReadNoPolling()
    {
        using var monitor = Build();

        // No event, no activation: the monitor must stay silent. There is no
        // timer to await — silence here proves event-driven capture.
        Assert.Equal(0, _reader.Calls);
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Activation_ChecksSequenceOnceAndCapturesOnChange()
    {
        using var monitor = Build();
        _sequences.Current = 7;
        _reader.Next = new ClipboardPayload(null, null, "just a note to self", []);

        monitor.OnActivated();

        Assert.Equal(1, _reader.Calls);
        Assert.Single(_store.List());
    }

    [Fact]
    public void Activation_SameSequence_SkipsRead()
    {
        using var monitor = Build();
        _sequences.Current = 7;
        _reader.Next = new ClipboardPayload(null, null, "just a note to self", []);
        monitor.OnActivated();
        Assert.Equal(1, _reader.Calls);

        _reader.Next = new ClipboardPayload(null, null, "something else", []);
        monitor.OnActivated();

        Assert.Equal(1, _reader.Calls);
        Assert.Single(_store.List());
    }

    [Fact]
    public void Notification_ThenActivationWithSameSequence_SkipsReRead()
    {
        using var monitor = Build();
        _sequences.Current = 7;
        _reader.Next = new ClipboardPayload(null, null, "just a note to self", []);

        _source.Raise();
        Assert.Equal(1, _reader.Calls);

        monitor.OnActivated();
        Assert.Equal(1, _reader.Calls);
        Assert.Single(_store.List());
    }

    [Fact]
    public void Notification_UsesForegroundProcessForExclusions()
    {
        var capture = new CaptureService(_store, new FileImageAssetStore(_imagesDir),
            new CaptureOptions
            {
                ExcludedProcesses = new HashSet<string>(["secretapp"], StringComparer.OrdinalIgnoreCase),
            },
            _clock);
        using var monitor = Build(capture);
        _processes.CurrentProcessName = "SecretApp.exe";
        _reader.Next = new ClipboardPayload(null, null, "nope", []);

        _source.Raise();

        Assert.Empty(_store.List());
    }
}
