// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Diagnostics;

// TEMPORARY instrumentation for ticket 20 (removed in ticket 25).
// File-only observer for the popup/paste/tray fix: proves where the popup
// opened, which paste target was captured, and how activation ended.
// Writes nowhere except a single file under %TEMP%; never touches UI,
// settings, history, or user behavior. Every line carries the unique
// "WCM20" prefix so smoke-ui.ps1 can grep it deterministically.
public static class TempSmokeLog
{
    public const string Prefix = "WCM20";

    public const string FileName = "WindowsCM-20-smoke.log";

    // Honours WINDOWS_CM_SMOKE_LOG when set (tests point it at a temp dir);
    // otherwise the single canonical file under %TEMP%.
    public static string LogPath
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("WINDOWS_CM_SMOKE_LOG");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return overridePath;
            }
            return Path.Combine(Path.GetTempPath(), FileName);
        }
    }

    private static readonly object Gate = new();

    public static void Write(string category, string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O} [{Prefix}:{category}] {message}{Environment.NewLine}";
            var path = LogPath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            lock (Gate)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Temporary diagnostics must never break the app.
        }
    }

    public static void Clear()
    {
        try
        {
            var path = LogPath;
            lock (Gate)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
        }
    }

    // Single-line content preview for logs: escapes newlines, truncates.
    public static string Preview(string? content, int maxLength = 80)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "<empty>";
        }
        var flat = content.Replace("\r", "\\r").Replace("\n", "\\n");
        return flat.Length <= maxLength ? flat : flat[..maxLength] + "…";
    }
}
