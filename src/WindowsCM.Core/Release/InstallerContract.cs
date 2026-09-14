// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Lifecycle;

namespace WindowsCM.Core.Release;

// Single source of truth shared by installer/WindowsCM.iss and the app
// (ticket 18, grilling 08 Q16). The .iss duplicates these values in its
// [Setup]/[Registry] sections and InstallerScriptTests pins them equal,
// so the installer can never drift from the runtime: the Run value name
// and the "<exe>" --hidden command shape are owned by AutostartManager,
// the data-dir names by AppFolders/UserDataPolicy.
public static class InstallerContract
{
    public const string AppName = "WindowsCM";
    public const string ExeName = "WindowsCM.exe";
    public const string AppVersion = "1.0.0";

    // Stable Inno AppId: changing it orphans upgrades (Inno treats a new
    // AppId as a different product and installs side by side).
    public const string AppId = "{5443123D-2460-456A-ABAA-B3ECD11A4134}";

    // Per-user install root under {localappdata} (grilling 08: no admin).
    public const string InstallSubPath = @"Programs\WindowsCM";

    // Win10 20H2 floor (spec target): build 19042. The .iss MinVersion
    // and the smoke matrix both pin this number.
    public const int MinWindowsBuild = 19042;

    public static string RunValueName => AutostartManager.RunValueName;

    // Not user data, so uninstall always removes them from %TEMP%: the
    // folder where the single-file exe unpacks its native DLLs (one
    // subfolder per build) and leftover incognito image folders.
    public const string BundleExtractSubPath = @".net\" + AppName;
    public static string IncognitoTempPrefix => EphemeralImageAssetStore.DirectoryPrefix;

    public static string ExpectedAutostartCommand(string installDir) =>
        AutostartManager.BuildCommand(Path.Combine(installDir, ExeName));
}
