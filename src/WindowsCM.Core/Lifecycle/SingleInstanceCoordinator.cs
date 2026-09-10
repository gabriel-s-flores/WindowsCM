// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

public enum SingleInstanceOutcome
{
    IsPrimary,
    Forwarded,
    ForwardFailed,
}

// Second-launch handoff (research 02 + 05 §10): the first instance owns the
// named mutex and serves the pipe; later launches forward one protocol line
// and exit. A no-arg second launch forwards "toggle" (show the popup). A
// failed forward never starts a second UI — the caller exits non-zero with
// diagnostics (stale-mutex servers recover via the abandoned-mutex path in
// the lock, not by doubling the UI).
public static class SingleInstanceCoordinator
{
    public static TimeSpan DefaultForwardTimeout { get; } = TimeSpan.FromSeconds(2);

    public static SingleInstanceOutcome Decide(
        ISingleInstanceLock mutex,
        IIpcForwarder forwarder,
        CliOptions cli,
        string pipeName,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(mutex);
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        if (mutex.TryAcquire())
        {
            return SingleInstanceOutcome.IsPrimary;
        }
        // Bare --hidden (autostart) has nothing to forward: exit quietly with
        // success, leaving the primary untouched and the UI hidden.
        var line = cli.FormatForForward();
        if (line is null)
        {
            return SingleInstanceOutcome.Forwarded;
        }
        var ok = forwarder.TryForward(pipeName, line, timeout ?? DefaultForwardTimeout, out _);
        return ok ? SingleInstanceOutcome.Forwarded : SingleInstanceOutcome.ForwardFailed;
    }
}
