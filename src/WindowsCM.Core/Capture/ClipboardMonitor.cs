// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

// Event-driven wiring: listener notifications in, CaptureService out. Never
// polls — the only sequence-number read is the punctual activation check
// (research 02: "should not be used in a polling loop").
public sealed class ClipboardMonitor : IDisposable
{
    private readonly IClipboardChangeSource _source;
    private readonly IClipboardReader _reader;
    private readonly CaptureService _capture;
    private readonly ISequenceProvider _sequences;
    private readonly IForegroundProcess _processes;
    private readonly IClock _clock;
    private uint _lastSequence;
    private bool _hasSequence;
    private bool _disposed;

    public ClipboardMonitor(
        IClipboardChangeSource source,
        IClipboardReader reader,
        CaptureService capture,
        ISequenceProvider sequences,
        IForegroundProcess processes,
        IClock clock)
    {
        _source = source;
        _reader = reader;
        _capture = capture;
        _sequences = sequences;
        _processes = processes;
        _clock = clock;
        _source.ClipboardChanged += OnClipboardChanged;
    }

    // Activation-time punctual check: capture only when the sequence moved.
    public void OnActivated()
    {
        var current = _sequences.GetSequenceNumber();
        if (_hasSequence && current == _lastSequence)
        {
            return;
        }
        _lastSequence = current;
        _hasSequence = true;
        CaptureCurrent();
    }

    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        CaptureCurrent();
        // Sync the activation baseline so the next OnActivated skips the
        // re-read; duplicates stay masked by CaptureService dedup regardless.
        _lastSequence = _sequences.GetSequenceNumber();
        _hasSequence = true;
    }

    private void CaptureCurrent()
    {
        var payload = _reader.Read();
        if (payload is null)
        {
            return;
        }
        _capture.Capture(payload, _processes.CurrentProcessName, _clock.UtcNow);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _source.ClipboardChanged -= OnClipboardChanged;
    }
}
