// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Release;

// What an upgrade or uninstall must never touch (ticket 18, grilling 08
// Q16: upgrades preserve data, uninstall keeps data unless the removal
// checkbox is set). The structural guarantee is disjointness: both data
// roots live outside the install root ({localappdata}\Programs\...), so
// replacing or deleting {app} cannot reach history, settings, actions,
// images or caches. The installer deletes the roots only via DelTree
// behind the uninstall checkbox (see installer/WindowsCM.iss [Code]).
public enum UninstallDataChoice
{
    KeepUserData,
    RemoveUserData,
}

public static class UserDataPolicy
{
    public static string DataRoot() => AppFolders.DataDir();

    public static string ConfigRoot() => AppFolders.ConfigDir();

    // Every default user-data location the product owns.
    public static IReadOnlyList<string> PreservedPaths() =>
    [
        DatabasePaths.Default(),
        AppFolders.SettingsPath(),
        ActionsPaths.Default(),
        AppFolders.ImagesDir(),
        AppFolders.CacheDir(),
    ];

    public static bool IsUserData(string path) =>
        IsUnder(path, DataRoot()) || IsUnder(path, ConfigRoot());

    public static bool IsUnderInstallRoot(string path, string installDir) =>
        IsUnder(path, installDir);

    public static bool ShouldDeleteUserData(UninstallDataChoice choice) =>
        choice == UninstallDataChoice.RemoveUserData;

    private static bool IsUnder(string path, string root)
    {
        var full = Normalize(path);
        var baseDir = Normalize(root);
        return full.Equals(baseDir, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
