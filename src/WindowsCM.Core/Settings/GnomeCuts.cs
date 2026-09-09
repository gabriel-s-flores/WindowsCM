// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// GNOME-only cuts decided in grilling 06 Q8 + research 04 §5-§6. Pinned so
// the settings window never reintroduces them: the UI hides these rows,
// and tests assert they stay absent from the ported models.
public static class GnomeCuts
{
    public static IReadOnlyList<string> Dropped { get; } =
    [
        "Yaru theme (GNOME-only; Windows offers Default/Custom)",
        "indicator-display (covered by the system tray)",
        "WM_CLASS matching (replaced by per-process exclusions)",
        "Sync primary (N/A on Win32: a single clipboard)",
        "Memory/JSON production backends (Memory is debug-only)",
        "Dependencies installer page (moved to About/Diagnostics)",
        "XDG locations (mapped to Windows Data/Config/Cache folders)",
        "paste-on-copy (deprecated upstream; use swap-copy-shortcut)",
    ];

    public static bool IsDropped(string feature) =>
        Dropped.Any(d => d.StartsWith(feature, StringComparison.OrdinalIgnoreCase));
}
