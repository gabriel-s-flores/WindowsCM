// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Microsoft.Data.Sqlite;
using WindowsCM.Core.Actions;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Settings;

// About/Diagnostics page (Copyous Dependencies→About remap, grilling 06
// Q8): library versions plus Data/Config/Cache shortcuts that the UI opens
// in Explorer. No process launching here — the WPF layer calls Explorer
// with these paths. Includes tray overflow guidance for Windows 11 (ticket 24).
public sealed record DiagnosticsInfo(
    string AppVersion,
    string DotNetVersion,
    string SqliteVersion,
    string DataDir,
    string ConfigDir,
    string CacheDir,
    string DatabasePath,
    string ActionsPath,
    string SettingsPath,
    string TrayGuidance = TrayOnboarding.Guidance)
{
    public static DiagnosticsInfo Collect(AppSettings? settings = null, string? settingsPath = null)
    {
        settings ??= new AppSettings();
        var app = typeof(AppSettings).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var dotnet = Environment.Version.ToString();
        var sqlite = typeof(SqliteConnection).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(SqliteConnection).Assembly.GetName().Version?.ToString()
            ?? "unknown";
        return new DiagnosticsInfo(
            app, dotnet, sqlite,
            AppFolders.DataDir(), AppFolders.ConfigDir(), AppFolders.CacheDir(),
            settings.History.ResolveDatabasePath(),
            ActionsPaths.Default(),
            settingsPath ?? AppFolders.SettingsPath(),
            TrayOnboarding.Guidance);
    }
}
