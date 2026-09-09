// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Hotkeys.Win32;

// Production registrar over user32 (research 03 §1.1/§1.7). Requires
// SetLastError so Marshal.GetLastWin32Error reports 1409 on conflicts.
// The HWND must belong to the calling thread; the UI attaches on
// SourceInitialized and unregisters on window close.
public sealed class Win32HotkeyRegistrar : IHotkeyRegistrar
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WmHotkey = 0x0312;

    public bool TryRegister(IntPtr hwnd, int id, uint modifiers, uint vk, out int error)
    {
        error = 0;
        if (RegisterHotKey(hwnd, id, modifiers, vk))
        {
            return true;
        }
        error = Marshal.GetLastWin32Error();
        return false;
    }

    public void Unregister(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);
}
