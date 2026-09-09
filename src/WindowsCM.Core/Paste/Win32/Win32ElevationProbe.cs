// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Paste.Win32;

// Token-elevation probe behind the UIPI diagnostics. Errors bias toward
// "elevated": OpenProcess failing on a visible window usually means a
// higher-integrity target, and a false positive only costs a copy-with-a-
// message while a false negative would be a silent paste failure.
public sealed class Win32ElevationProbe : IElevationProbe
{
    public Win32ElevationProbe()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Elevation probing requires Windows.");
        }
    }

    public bool IsCurrentProcessElevated() =>
        IsHandleElevated(NativePaste.GetCurrentProcess());

    public bool IsTargetElevated(IntPtr hwnd)
    {
        if (NativePaste.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
        {
            return false;
        }
        var process = NativePaste.OpenProcess(
            NativePaste.PROCESS_QUERY_INFORMATION, false, pid);
        if (process == IntPtr.Zero)
        {
            return true; // Suspect: a visible but unopenable process.
        }
        try
        {
            return IsHandleElevated(process);
        }
        finally
        {
            NativePaste.CloseHandle(process);
        }
    }

    // Never closes the process handle: the current-process pseudo-handle
    // must stay open and opened handles are closed by their owner above.
    private static bool IsHandleElevated(IntPtr process)
    {
        if (!NativePaste.OpenProcessToken(
                process, NativePaste.TOKEN_QUERY, out var token))
        {
            return true; // Suspect: token query refused.
        }
        try
        {
            var elevation = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                if (!NativePaste.GetTokenInformation(
                        token, NativePaste.TokenElevation,
                        elevation, sizeof(int), out _))
                {
                    return true; // Suspect: elevation unreadable.
                }
                return Marshal.ReadInt32(elevation) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(elevation);
            }
        }
        finally
        {
            NativePaste.CloseHandle(token);
        }
    }
}
