// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace WindowsCM.Core.Capture.Win32;

// Foreground process for exclusions. Null when unknown: fail open (capture).
public sealed class Win32ForegroundProcess : IForegroundProcess
{
    public string? CurrentProcessName
    {
        get
        {
            try
            {
                var hwnd = NativeClipboard.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return null;
                }
                NativeClipboard.GetWindowThreadProcessId(hwnd, out var pid);
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName + ".exe";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
