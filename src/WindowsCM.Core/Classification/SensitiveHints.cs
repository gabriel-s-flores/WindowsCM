// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Classification;

// Formats whose presence means the copy must never enter history.
public static class SensitiveHints
{
    // GNOME password-manager hint honored by Copyous.
    public const string KdePasswordHint = "x-kde-passwordManagerHint";

    // Windows opt-out honored by clipboard-history-aware apps.
    public const string WindowsExcludeFromMonitor = "ExcludeClipboardContentFromMonitorProcessing";

    public static bool ContainsSensitive(IEnumerable<string> formats) =>
        formats.Any(f => f == KdePasswordHint || f == WindowsExcludeFromMonitor);
}
