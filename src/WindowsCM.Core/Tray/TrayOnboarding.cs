// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// Guidance for Windows 11 system tray overflow drawer (research 03, ticket 24).
// In Windows 11, new tray icons start in the hidden overflow flyout (^) by default.
// Microsoft provides no supported API to programmatically promote icons onto the
// main taskbar, so WindowsCM guides the user to drag the icon out of the drawer
// rather than attempting fragile or prohibited hacks.
public static class TrayOnboarding
{
    public const string Guidance =
        "On Windows 11, new tray icons appear in the taskbar overflow drawer (^) by default.\n" +
        "To keep WindowsCM always visible next to the clock, click the ^ chevron on your taskbar " +
        "and drag the WindowsCM icon onto the taskbar (or enable it in Windows Settings > Personalization > Taskbar > Other system tray icons).\n" +
        "Programmatic promotion is not attempted by design, adhering to Windows platform rules.";

    public const string GuidanceSummary =
        "Drag WindowsCM icon from overflow (^) to taskbar. Programmatic promotion is not attempted.";
}
