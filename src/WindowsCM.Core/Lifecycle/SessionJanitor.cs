// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Lifecycle;

// Session-end cleanup (research 05 §4, spec Janitor): runs the
// EndOfSession mode synchronously on SessionEnding, under one second, never
// cancels logout, and unsubscribes on dispose (static events leak
// otherwise). The mode is read live per event so the settings toggle
// applies without restart. Store failures are best-effort swallowed: a
// throwing cleanup must never block logoff. Requires a WPF message pump in
// production; the handler itself does no dispatching.
public sealed class SessionJanitor : IDisposable
{
    private readonly ISessionEndingSource _source;
    private readonly IHistoryStore _store;
    private readonly Func<EndOfSessionMode> _mode;
    private bool _disposed;

    public SessionJanitor(ISessionEndingSource source, IHistoryStore store, Func<EndOfSessionMode> mode)
    {
        _source = source;
        _store = store;
        _mode = mode;
        _source.SessionEnding += OnSessionEnding;
    }

    public SessionJanitor(ISessionEndingSource source, IHistoryStore store, EndOfSessionMode mode)
        : this(source, store, () => mode)
    {
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        // Never cancel: respect the user's logoff intent (Learn default
        // DefWindowProc returns TRUE; e.Cancel=true only begs to continue).
        try
        {
            var current = _mode();
            if (SessionCleanup.ShouldClear(current))
            {
                SessionCleanup.Apply(current, _store);
            }
        }
        catch
        {
            // Best-effort during logoff: swallow, never block shutdown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _source.SessionEnding -= OnSessionEnding;
    }
}
