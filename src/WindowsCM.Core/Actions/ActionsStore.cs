// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace WindowsCM.Core.Actions;

// actions.json location. Precedence mirrors DatabasePaths: the environment
// override (debug/automation) first, then an explicit setting, then the
// roaming default (%AppData%\WindowsCM\actions.json preserves the XDG-config
// semantics of the original). The "default" sentinel forces built-ins
// without touching disk (Copyous DEBUG_COPYOUS_ACTIONS=default parity).
public static class ActionsPaths
{
    public const string EnvVariable = "WINDOWSCM_ACTIONS_PATH";
    public const string LegacyEnvVariable = "DEBUG_COPYOUS_ACTIONS";
    public const string DefaultSentinel = "default";

    public static string Default() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsCM",
        "actions.json");

    public static string Resolve(string? settingPath)
    {
        // DatabasePaths parity: the environment (debug/automation) wins
        // over the configured setting.
        var env = Environment.GetEnvironmentVariable(EnvVariable)
            ?? Environment.GetEnvironmentVariable(LegacyEnvVariable);
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
}

// File-backed action config (Copyous `loadConfig`/`saveConfig` parity).
// The store is deliberately instance-free and stateless: live reload
// (FileSystemWatcher) belongs to the UI layer, which calls Load again.
public static class ActionsStore
{
    // Missing file returns built-ins (saving them only when asked, so a
    // first read never creates disk state as a side effect). A corrupt
    // file degrades to built-ins, never to a half-loaded config.
    public static ActionConfig Load(string path, bool saveDefault = false)
    {
        if (string.Equals(path, ActionsPaths.DefaultSentinel, StringComparison.OrdinalIgnoreCase))
        {
            return BuiltinActions.Default();
        }
        if (!File.Exists(path))
        {
            var fresh = BuiltinActions.Default();
            if (saveDefault)
            {
                Save(path, fresh);
            }
            return fresh;
        }
        try
        {
            return ActionsJson.Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return BuiltinActions.Default();
        }
    }

    // Atomic write (tmp + move) with tab indentation (Copyous
    // JSON.stringify(config, null, '\t') parity). Backup keeps a
    // "<file>~" copy of the previous config (Gio REPLACE + backup parity),
    // used by Restore/Reset before they overwrite customs.
    public static void Save(string path, ActionConfig config, bool backup = false)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (backup && File.Exists(path))
        {
            File.Copy(path, path + "~", overwrite: true);
        }
        var tabbed = ToTabIndented(ActionsJson.Serialize(config));
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, tabbed);
        File.Move(temp, path, overwrite: true);
    }

    // Restore merges built-ins missing by id, preserving customs and the
    // user's defaults; Reset returns to the integral default config.
    public static ActionConfig Restore(ActionConfig current) =>
        BuiltinActions.MergeMissing(current, BuiltinActions.Default());

    public static ActionConfig Reset() => BuiltinActions.Default();

    // System.Text.Json indents with two spaces; Copyous indents with tabs.
    // Each leading 2-space group is one level, so the mapping is exact.
    internal static string ToTabIndented(string json)
    {
        var lines = json.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var depth = 0;
            while (lines[i].StartsWith(new string(' ', (depth + 1) * 2), StringComparison.Ordinal))
            {
                depth++;
            }
            if (depth > 0)
            {
                lines[i] = new string('\t', depth) + lines[i][(depth * 2)..];
            }
        }
        return string.Join('\n', lines);
    }
}
