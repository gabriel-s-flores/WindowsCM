// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle.Win32;

// Production named-mutex lock (research 02 + 05 §10). Deviation from the
// research literal `new Mutex(false, ...)`: initial ownership is requested
// (true) so an abandoned previous owner surfaces as
// AbandonedMutexException — which still means "we are now primary" (stale
// mutex becomes the server). Existence-only handles cannot detect that.
public sealed class MutexSingleInstanceLock : ISingleInstanceLock
{
    private readonly string _name;
    private Mutex? _mutex;
    private bool _disposed;

    public MutexSingleInstanceLock(string mutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        _name = mutexName;
    }

    public bool IsAcquired => _mutex is not null;

    public bool TryAcquire()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MutexSingleInstanceLock));
        }
        if (_mutex is not null)
        {
            return true;
        }
        try
        {
            _mutex = new Mutex(true, _name, out var createdNew);
            if (createdNew)
            {
                return true;
            }
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (AbandonedMutexException ex)
        {
            // The previous owner died without releasing: the OS transfers
            // ownership to us, so we are primary (stale mutex becomes the
            // server). A null handle honestly reports "not acquired".
            _mutex = ex.Mutex as Mutex;
            return _mutex is not null;
        }
    }

    public void Release()
    {
        var mutex = _mutex;
        _mutex = null;
        if (mutex is null)
        {
            return;
        }
        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owner (abandoned path already transferred): disposal
            // still frees the handle.
        }
        mutex.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Release();
    }
}
