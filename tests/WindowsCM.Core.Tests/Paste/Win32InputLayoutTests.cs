// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using WindowsCM.Core.Paste.Win32;

namespace WindowsCM.Core.Tests.Paste;

// Ticket 22 regression: SendInput validates cbSize against the platform
// sizeof(INPUT) (40 on x64, 28 on x86). A too-narrow INPUTUNION (keyboard
// arm only) undershoots it, so every paste chord is rejected and every
// Enter ends in Failed instead of Pasted. Pure marshalling math, no input
// is ever injected.
public sealed class Win32InputLayoutTests
{
    [Fact]
    public void InputSize_MatchesPlatformWin32Input()
    {
        var expected = Environment.Is64BitProcess ? 40 : 28;

        Assert.Equal(expected, Marshal.SizeOf<NativePaste.INPUT>());
    }

    [Fact]
    public void Union_IsAsWideAsMouseInput()
    {
        Assert.Equal(
            Marshal.SizeOf<NativePaste.MOUSEINPUT>(),
            Marshal.SizeOf<NativePaste.INPUTUNION>());
    }
}
