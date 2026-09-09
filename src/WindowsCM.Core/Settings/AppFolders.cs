// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Settings;

// Windows folder map (XDG → Windows, research 05 §1 + 04 §4): Data holds
// the DB and images, Config holds actions.json + settings.json, Cache
// holds link thumbnails. The About/Diagnostics page opens these in
// Explorer; Core only resolves them so the paths are test-pinned.
public static class AppFolders
{
    public static string DataDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsCM");

    public static string ConfigDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsCM");

    public static string CacheDir() => Path.Combine(DataDir(), "Cache");

    public static string ImagesDir() => Path.Combine(DataDir(), "images");

    public static string LinkImagesDir() => LinkImageCache.DefaultDirectory();

    public static string SettingsPath() => Path.Combine(ConfigDir(), "settings.json");

    public static string DatabaseDefault() => DatabasePaths.Default();

    public static string ActionsDefault() => ActionsPaths.Default();

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir());
        Directory.CreateDirectory(ConfigDir());
        Directory.CreateDirectory(CacheDir());
    }
}
