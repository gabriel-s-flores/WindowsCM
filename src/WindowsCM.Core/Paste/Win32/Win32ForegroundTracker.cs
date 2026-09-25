// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace WindowsCM.Core.Paste.Win32;

// Remembers the last window the user worked in (an app or the desktop, per
// PasteTargetPolicy.ShouldRemember) through EVENT_SYSTEM_FOREGROUND, so a
// popup opened from the tray pastes into the field the user had clicked
// before reaching the taskbar. The hook is out-of-context: callbacks run on
// the creating thread's message loop (the WPF UI thread), never inside
// another process. Construct it on that thread.
public sealed class Win32ForegroundTracker : IDisposable
{
    private readonly NativePaste.WinEventProc _callback;
    private readonly object _gate = new();
    private IntPtr _hook;
    private ForegroundSnapshot _lastOutside = ForegroundSnapshot.None;

    public Win32ForegroundTracker()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Foreground tracking requires Windows.");
        }
        // Kept in a field: the hook calls back through this delegate for
        // its whole lifetime, so it must never be collected.
        _callback = OnForegroundChanged;
        Remember(Describe(NativePaste.GetForegroundWindow()));
        _hook = NativePaste.SetWinEventHook(
            NativePaste.EVENT_SYSTEM_FOREGROUND, NativePaste.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0,
            NativePaste.WINEVENT_OUTOFCONTEXT | NativePaste.WINEVENT_SKIPOWNPROCESS);
    }

    public ForegroundSnapshot LastOutside
    {
        get
        {
            lock (_gate)
            {
                return _lastOutside;
            }
        }
    }

    // Where a pick should paste right now; Zero means copy only. Safe from
    // any thread (pipe commands show the popup from pool threads).
    public IntPtr ResolveTarget()
    {
        var current = Describe(NativePaste.GetForegroundWindow());
        var last = LastOutside;
        if (last.Handle != IntPtr.Zero && !NativePaste.IsWindow(last.Handle))
        {
            last = ForegroundSnapshot.None;
        }
        return PasteTargetPolicy.Resolve(current, last);
    }

    public static ForegroundSnapshot Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return ForegroundSnapshot.None;
        }
        NativePaste.GetWindowThreadProcessId(hwnd, out var processId);
        var className = new StringBuilder(256);
        var length = NativePaste.GetClassNameW(hwnd, className, className.Capacity);
        var role = PasteTargetPolicy.Classify(
            hwnd,
            length > 0 ? className.ToString() : null,
            ownProcess: processId == (uint)Environment.ProcessId);
        return new ForegroundSnapshot(hwnd, role);
    }

    private void OnForegroundChanged(
        IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint eventTime)
    {
        // Called from native code: nothing may escape.
        try
        {
            Remember(Describe(hwnd));
        }
        catch
        {
        }
    }

    private void Remember(ForegroundSnapshot snapshot)
    {
        if (!PasteTargetPolicy.ShouldRemember(snapshot.Role))
        {
            return;
        }
        lock (_gate)
        {
            _lastOutside = snapshot;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativePaste.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
