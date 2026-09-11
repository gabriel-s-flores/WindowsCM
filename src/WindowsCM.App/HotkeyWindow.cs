// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Interop;
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Hotkeys.Win32;

namespace WindowsCM.App;

// Invisible HWND owning the global chords. RegisterHotKey needs a handle
// on the calling thread, so this window lives on the UI thread forever;
// WM_HOTKEY arrives via the HwndSource hook with the slot id in wParam
// (HotkeyService.SlotForId parity with the registration ids).
internal sealed class HotkeyWindow : Window
{
    private readonly Action<HotkeySlot> _onHotkey;
    private HwndSource? _source;

    public HotkeyWindow(Action<HotkeySlot> onHotkey)
    {
        _onHotkey = onHotkey;
        Width = 0;
        Height = 0;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Visibility = Visibility.Hidden;
    }

    public IntPtr Handle { get; private set; }

    public IntPtr EnsureHandle()
    {
        if (Handle != IntPtr.Zero)
        {
            return Handle;
        }
        Handle = new WindowInteropHelper(this).EnsureHandle();
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(WndProc);
        return Handle;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32HotkeyRegistrar.WmHotkey
            && HotkeyService.SlotForId(wParam.ToInt32()) is { } slot)
        {
            _onHotkey(slot);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
