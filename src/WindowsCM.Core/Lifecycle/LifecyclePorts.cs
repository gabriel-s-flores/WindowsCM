// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// Named-mutex ownership. Production wraps System.Threading.Mutex
// (MutexSingleInstanceLock); tests fake it or mint real mutexes with GUID
// names. Abandoned previous owners count as acquirable (stale mutex
// becomes the new server, research 05 §10).
public interface ISingleInstanceLock : IDisposable
{
    bool IsAcquired { get; }
    bool TryAcquire();
    void Release();
}

// Pipe client used for second-instance handoff. Production dials the
// NamedPipeServerStream; tests fake the answer. Returns false (no throw)
// when no server listens so the caller can exit non-zero with guidance.
public interface IIpcForwarder
{
    bool TryForward(string pipeName, string line, TimeSpan timeout, out string? response);
}

// HKCU\...\Run persistence behind autostart. Production P/Invokes Advapi32
// (RegistryRunKeyStore); tests use MemoryRunKeyStore. Null means absent.
public interface IRunKeyStore
{
    string? GetCommand();
    void SetCommand(string command);
    void Remove();
}

public sealed class MemoryRunKeyStore : IRunKeyStore
{
    private string? _command;

    public string? GetCommand() => _command;

    public void SetCommand(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _command = command;
    }

    public void Remove() => _command = null;
}

// Session-ending notifications (Microsoft.Win32.SystemEvents.SessionEnding
// in production WPF; a message pump is required for the event to fire,
// research 05 §4). Cancel stays false: cleanup never blocks logout.
public sealed class SessionEndingEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}

public interface ISessionEndingSource
{
    event EventHandler<SessionEndingEventArgs>? SessionEnding;
}
