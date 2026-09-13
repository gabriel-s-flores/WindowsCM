// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Hotkeys.Win32;

// Production layout lookups over user32. Chars resolve against the calling
// thread's active layout first, then every other installed layout, so a
// gesture saved on one layout still parses after switching to another.
public sealed class Win32KeyboardLayout : IKeyboardLayout
{
    private const uint MapVkToVsc = 0;
    // Win10 1607+: translate without touching the kernel dead-key state.
    private const uint ToUnicodeNoStateChange = 0x4;

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[]? lpList);

    [DllImport("user32.dll", EntryPoint = "VkKeyScanExW", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out] char[] pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    public uint? VirtualKeyForChar(char c)
    {
        var candidates = new[] { char.ToLowerInvariant(c), c, char.ToUpperInvariant(c) }.Distinct().ToArray();
        foreach (var hkl in Layouts())
        {
            foreach (var candidate in candidates)
            {
                var scan = VkKeyScanEx(candidate, hkl);
                if (scan == -1)
                {
                    continue;
                }
                var shiftState = (scan >> 8) & 0xFF;
                // Ctrl/Alt bits mean AltGr: the key alone does not type it.
                if ((shiftState & 0x06) != 0)
                {
                    continue;
                }
                // Shifted symbols ('!' is Shift+1) would register as the bare key.
                if ((shiftState & 0x01) != 0 && !char.IsLetter(candidate))
                {
                    continue;
                }
                return (uint)(scan & 0xFF);
            }
        }
        return null;
    }

    public char? CharForVirtualKey(uint virtualKey)
    {
        var hkl = GetKeyboardLayout(0);
        var scanCode = MapVirtualKeyEx(virtualKey, MapVkToVsc, hkl);
        var buffer = new char[8];
        var written = ToUnicodeEx(virtualKey, scanCode, new byte[256], buffer, buffer.Length,
            ToUnicodeNoStateChange, hkl);
        // -1 is a dead key (´ ~ ^): its spacing char is still in the buffer.
        return written is 1 or -1 ? buffer[0] : null;
    }

    private static IEnumerable<IntPtr> Layouts()
    {
        var active = GetKeyboardLayout(0);
        yield return active;
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0)
        {
            yield break;
        }
        var installed = new IntPtr[count];
        count = GetKeyboardLayoutList(installed.Length, installed);
        for (var i = 0; i < count; i++)
        {
            if (installed[i] != active)
            {
                yield return installed[i];
            }
        }
    }
}
