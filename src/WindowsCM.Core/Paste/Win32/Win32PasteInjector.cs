// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Paste.Win32;

// Injects the paste chord into the foreground app (research 02 § "Colar"):
// Ctrl+V by default (Ctrl down, V down/up, Ctrl up), Shift+Insert as the
// manual opt-in. UIPI blocks delivery to higher-integrity apps without any
// signal in the return value — that refusal is diagnosed by the elevation
// probe before injection, never here. Manual-smoke per Testing Decisions.
public sealed class Win32PasteInjector : IPasteInjector
{
    public Win32PasteInjector()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("SendInput requires Windows.");
        }
    }

    public void Inject(PasteSequence sequence)
    {
        var (modifier, key) = sequence == PasteSequence.ShiftInsert
            ? (NativePaste.VK_SHIFT, NativePaste.VK_INSERT)
            : (NativePaste.VK_CONTROL, NativePaste.VK_V);
        var inputs = new[]
        {
            Key(modifier, up: false),
            Key(key, up: false),
            Key(key, up: true),
            Key(modifier, up: true),
        };
        var sent = NativePaste.SendInput(
            (uint)inputs.Length, inputs, Marshal.SizeOf<NativePaste.INPUT>());
        if (sent != inputs.Length)
        {
            throw new PasteInjectionException(
                "The paste chord was not accepted for injection.");
        }
    }

    private static NativePaste.INPUT Key(ushort virtualKey, bool up) => new()
    {
        Type = NativePaste.INPUT_KEYBOARD,
        U = new NativePaste.INPUTUNION
        {
            Ki = new NativePaste.KEYBDINPUT
            {
                WVk = virtualKey,
                DwFlags = up ? NativePaste.KEYEVENTF_KEYUP : 0,
            },
        },
    };
}
