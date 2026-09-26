// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// What kind of window held the foreground when the popup was asked for.
public enum WindowRole
{
    // No window (or one that vanished).
    None,
    // A regular application window: somewhere a paste can land.
    App,
    // WindowsCM itself (popup, settings, the tray menu).
    Own,
    // Taskbar, tray overflow, Start/Search and switcher surfaces: the user
    // passes through them to open WindowsCM, they are never the target.
    ShellChrome,
    // The desktop: clicking it means "nothing to paste into".
    Desktop,
}

public readonly record struct ForegroundSnapshot(IntPtr Handle, WindowRole Role)
{
    public static ForegroundSnapshot None { get; } = new(IntPtr.Zero, WindowRole.None);
}

// Which window a pick pastes into. Opening from the hotkey keeps the app the
// user was typing in. Opening from the tray icon (or the tray menu) makes
// the taskbar/our own menu the foreground, so the target falls back to the
// last window the user was in before reaching the tray — otherwise the
// paste aimed at the taskbar and the item never reached the field the user
// had clicked. When that last place is the desktop, or there is none, the
// target is Zero: the pick is copied, never pasted into a guessed window.
public static class PasteTargetPolicy
{
    private static readonly HashSet<string> ShellChromeClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",                       // primary taskbar
        "Shell_SecondaryTrayWnd",              // taskbar on other monitors
        "NotifyIconOverflowWindow",            // Windows 10 tray overflow
        "TopLevelWindowForOverflowXamlIsland", // Windows 11 tray overflow
        "Windows.UI.Core.CoreWindow",          // Start, Search, Action Center
        "XamlExplorerHostIslandWindow",        // Windows 11 shell flyouts
        "MultitaskingViewFrame",               // Alt+Tab / Task View
        "TaskSwitcherWnd",
        "ForegroundStaging",                   // transient shell staging window
    };

    private static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
    };

    public static WindowRole Classify(IntPtr handle, string? className, bool ownProcess)
    {
        if (handle == IntPtr.Zero)
        {
            return WindowRole.None;
        }
        if (ownProcess)
        {
            return WindowRole.Own;
        }
        if (className is not null && ShellChromeClasses.Contains(className))
        {
            return WindowRole.ShellChrome;
        }
        if (className is not null && DesktopClasses.Contains(className))
        {
            return WindowRole.Desktop;
        }
        return WindowRole.App;
    }

    // The foreground history keeps the last place the user actually was:
    // apps and the desktop count, the shell surfaces and WindowsCM do not.
    public static bool ShouldRemember(WindowRole role) =>
        role is WindowRole.App or WindowRole.Desktop;

    public static IntPtr Resolve(ForegroundSnapshot current, ForegroundSnapshot lastOutside) =>
        current.Role switch
        {
            WindowRole.App => current.Handle,
            WindowRole.Own or WindowRole.ShellChrome =>
                lastOutside.Role == WindowRole.App ? lastOutside.Handle : IntPtr.Zero,
            _ => IntPtr.Zero,
        };
}
