// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.History;

// Database location resolution. Precedence: environment (debug/automation),
// then the configured setting, then the LocalAppData default.
public static class DatabasePaths
{
    public const string EnvVariable = "WINDOWSCM_DBPATH";

    public static string Default() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsCM",
        "clipboard.db");

    public static string Resolve(string? settingPath)
    {
        var env = Environment.GetEnvironmentVariable(EnvVariable);
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }
        if (!string.IsNullOrEmpty(settingPath))
        {
            return settingPath;
        }
        return Default();
    }

    // Null when the path is usable: .db extension with an existing parent
    // directory. Otherwise a human-readable reason (shown, never thrown).
    public static string? Validate(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase))
        {
            return $"Database path must end in .db: {path}";
        }
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            return $"Database directory does not exist: {path}";
        }
        return null;
    }
}
