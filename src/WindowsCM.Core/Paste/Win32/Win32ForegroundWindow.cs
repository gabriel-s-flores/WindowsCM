// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste.Win32;

// Live foreground reads. The UI captures GetForegroundWindow() at hotkey
// time; the orchestrator compares it here before injecting.
public sealed class Win32ForegroundWindow : IForegroundWindow
{
    public Win32ForegroundWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Foreground tracking requires Windows.");
        }
    }

    public IntPtr GetCurrent() => NativePaste.GetForegroundWindow();

    public bool RestoreForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativePaste.IsWindow(hwnd))
        {
            return false;
        }
        // A hung target (not pumping messages for 5 s) would make
        // AttachThreadInput tie our thread to its frozen input queue and
        // hang WindowsCM with it. Refuse instead: the orchestrator reports
        // "focus lost" and the item stays on the clipboard.
        if (NativePaste.IsHungAppWindow(hwnd))
        {
            return false;
        }

        var currentForeground = NativePaste.GetForegroundWindow();
        if (currentForeground == hwnd)
        {
            return true;
        }

        var targetThread = NativePaste.GetWindowThreadProcessId(hwnd, out _);
        var currentThread = NativePaste.GetCurrentThreadId();

        var attached = false;
        if (targetThread != 0 && targetThread != currentThread)
        {
            attached = NativePaste.AttachThreadInput(currentThread, targetThread, true);
        }

        try
        {
            return NativePaste.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativePaste.AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }
}
