// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Diagnostics;

// Decides whether the UI keeps running after an unhandled exception on the
// dispatcher. For a tray app an isolated failure (one bad card, a busy
// clipboard, a missing file) must not end the session — before this every
// such error closed the app. A burst, though, means a per-frame failure
// (layout, render) that would spin forever, so past MaxErrors inside
// Window the app is allowed to terminate.
public sealed class UnhandledErrorPolicy
{
    private readonly Queue<DateTime> _recent = new();
    private readonly object _gate = new();

    public UnhandledErrorPolicy(int maxErrors = 5, TimeSpan? window = null)
    {
        MaxErrors = Math.Max(1, maxErrors);
        Window = window ?? TimeSpan.FromSeconds(10);
    }

    public int MaxErrors { get; }

    public TimeSpan Window { get; }

    public bool ShouldContinue(DateTime utcNow)
    {
        lock (_gate)
        {
            while (_recent.Count > 0 && utcNow - _recent.Peek() > Window)
            {
                _recent.Dequeue();
            }
            _recent.Enqueue(utcNow);
            return _recent.Count <= MaxErrors;
        }
    }
}
