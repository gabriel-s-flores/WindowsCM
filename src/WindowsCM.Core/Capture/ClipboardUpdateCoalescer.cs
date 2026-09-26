// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

// When to read after WM_CLIPBOARDUPDATE. Reading on the notification itself
// races the source app, which is often still finishing its copy (OLE sets
// the data, then flushes/renders it): opening the clipboard in between made
// both sides wait on each other with the clipboard held, and the real-app
// smoke measured 131 of 300 copies in another app failing while WindowsCM
// ran (0 of 60 without it). Reads now wait for a short quiet period; a
// burst of updates coalesces into one read of the final content, capped so
// a source that keeps updating cannot postpone the read forever.
public sealed class ClipboardUpdateCoalescer
{
    public static readonly TimeSpan DefaultQuiet = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan DefaultMaxWait = TimeSpan.FromMilliseconds(500);

    private readonly long _quietMs;
    private readonly long _maxWaitMs;
    private long? _firstPendingMs;

    public ClipboardUpdateCoalescer(TimeSpan? quiet = null, TimeSpan? maxWait = null)
    {
        _quietMs = (long)(quiet ?? DefaultQuiet).TotalMilliseconds;
        _maxWaitMs = Math.Max(_quietMs, (long)(maxWait ?? DefaultMaxWait).TotalMilliseconds);
    }

    public bool IsPending => _firstPendingMs is not null;

    // An update arrived: returns the delay (ms) to (re)arm the read timer
    // with — the quiet period, shortened so the first update of a burst is
    // never read later than maxWait after it arrived.
    public int OnUpdate(long nowMs)
    {
        _firstPendingMs ??= nowMs;
        var untilCap = _firstPendingMs.Value + _maxWaitMs - nowMs;
        return (int)Math.Clamp(Math.Min(_quietMs, untilCap), 0, int.MaxValue);
    }

    // The timer fired and the read happens now; the next update opens a
    // new window.
    public void OnRead() => _firstPendingMs = null;
}
