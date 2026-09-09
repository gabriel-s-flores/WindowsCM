// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// The five tray menu items (research 03 §3.2, map Q4). Labels are English
// in v1 per the spec; Clear keeps pins+tags (never wipe-all from the tray).
public enum TrayMenuItem
{
    Open,
    Incognito,
    Clear,
    Settings,
    Exit,
}

public static class TrayMenu
{
    public static IReadOnlyList<TrayMenuItem> All { get; } =
    [
        TrayMenuItem.Open,
        TrayMenuItem.Incognito,
        TrayMenuItem.Clear,
        TrayMenuItem.Settings,
        TrayMenuItem.Exit,
    ];

    public static string LabelFor(TrayMenuItem item) => item switch
    {
        TrayMenuItem.Open => "Open",
        TrayMenuItem.Incognito => "Incognito",
        TrayMenuItem.Clear => "Clear (keep pins and tags)",
        TrayMenuItem.Settings => "Settings",
        TrayMenuItem.Exit => "Exit",
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
