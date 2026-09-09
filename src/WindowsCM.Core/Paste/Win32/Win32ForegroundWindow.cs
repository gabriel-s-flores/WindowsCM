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
}
