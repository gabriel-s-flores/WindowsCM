// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

public sealed class PasteTargetPolicyTests
{
    private static readonly IntPtr Notepad = new(0x100);
    private static readonly IntPtr Taskbar = new(0x200);
    private static readonly IntPtr Desktop = new(0x300);
    private static readonly IntPtr OwnPopup = new(0x400);

    [Theory]
    [InlineData("Notepad", false, WindowRole.App)]
    [InlineData("Chrome_WidgetWin_1", false, WindowRole.App)]
    [InlineData("CabinetWClass", false, WindowRole.App)]
    [InlineData("Shell_TrayWnd", false, WindowRole.ShellChrome)]
    [InlineData("Shell_SecondaryTrayWnd", false, WindowRole.ShellChrome)]
    [InlineData("NotifyIconOverflowWindow", false, WindowRole.ShellChrome)]
    [InlineData("TopLevelWindowForOverflowXamlIsland", false, WindowRole.ShellChrome)]
    [InlineData("Windows.UI.Core.CoreWindow", false, WindowRole.ShellChrome)]
    [InlineData("Progman", false, WindowRole.Desktop)]
    [InlineData("WorkerW", false, WindowRole.Desktop)]
    [InlineData("HwndWrapper[WindowsCM;;x]", true, WindowRole.Own)]
    [InlineData("Shell_TrayWnd", true, WindowRole.Own)]
    [InlineData(null, false, WindowRole.App)]
    public void Classify_SortsWindowsByRole(string? className, bool ownProcess, WindowRole expected)
    {
        Assert.Equal(expected, PasteTargetPolicy.Classify(new IntPtr(1), className, ownProcess));
    }

    [Fact]
    public void Classify_NoWindow_IsNone()
    {
        Assert.Equal(WindowRole.None, PasteTargetPolicy.Classify(IntPtr.Zero, "Notepad", ownProcess: false));
    }

    [Fact]
    public void Hotkey_FromAnApp_PastesIntoThatApp()
    {
        var target = PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(Notepad, WindowRole.App),
            new ForegroundSnapshot(Desktop, WindowRole.Desktop));

        Assert.Equal(Notepad, target);
    }

    [Fact]
    public void TrayClick_AfterClickingAField_PastesIntoTheFieldsApp()
    {
        // Clicking the tray icon makes the taskbar the foreground; the
        // field the user had clicked lives in the last app before it.
        var target = PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(Taskbar, WindowRole.ShellChrome),
            new ForegroundSnapshot(Notepad, WindowRole.App));

        Assert.Equal(Notepad, target);
    }

    [Fact]
    public void TrayMenu_OwnWindowForeground_FallsBackToTheLastApp()
    {
        var target = PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(OwnPopup, WindowRole.Own),
            new ForegroundSnapshot(Notepad, WindowRole.App));

        Assert.Equal(Notepad, target);
    }

    [Fact]
    public void LastClickOnTheDesktop_NothingToPasteInto()
    {
        Assert.Equal(IntPtr.Zero, PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(Desktop, WindowRole.Desktop),
            new ForegroundSnapshot(Notepad, WindowRole.App)));
        Assert.Equal(IntPtr.Zero, PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(Taskbar, WindowRole.ShellChrome),
            new ForegroundSnapshot(Desktop, WindowRole.Desktop)));
    }

    [Fact]
    public void NoHistory_NothingToPasteInto()
    {
        Assert.Equal(IntPtr.Zero, PasteTargetPolicy.Resolve(
            new ForegroundSnapshot(Taskbar, WindowRole.ShellChrome), ForegroundSnapshot.None));
        Assert.Equal(IntPtr.Zero, PasteTargetPolicy.Resolve(ForegroundSnapshot.None, ForegroundSnapshot.None));
    }

    [Theory]
    [InlineData(WindowRole.App, true)]
    [InlineData(WindowRole.Desktop, true)]
    [InlineData(WindowRole.ShellChrome, false)]
    [InlineData(WindowRole.Own, false)]
    [InlineData(WindowRole.None, false)]
    public void ShouldRemember_OnlyPlacesTheUserWorkedIn(WindowRole role, bool expected)
    {
        Assert.Equal(expected, PasteTargetPolicy.ShouldRemember(role));
    }
}
