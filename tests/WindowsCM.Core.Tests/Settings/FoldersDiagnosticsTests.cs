// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Folders + About/Diagnostics: the page shows versions and opens
// Data/Config/Cache in Explorer (Core resolves, WPF launches).
public sealed class FoldersDiagnosticsTests
{
    [Fact]
    public void Folders_DataConfigCacheAreDistinctUnderUserProfile()
    {
        var data = AppFolders.DataDir();
        var config = AppFolders.ConfigDir();
        var cache = AppFolders.CacheDir();

        Assert.NotEqual(data, config);
        Assert.StartsWith(data, cache, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("WindowsCM", data.TrimEnd(Path.DirectorySeparatorChar));
        Assert.EndsWith("WindowsCM", config.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Folders_SettingsLivesUnderConfig_DatabaseUnderData()
    {
        Assert.StartsWith(AppFolders.ConfigDir(), AppFolders.SettingsPath(), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("settings.json", AppFolders.SettingsPath());
        Assert.StartsWith(AppFolders.DataDir(), AppFolders.DatabaseDefault(), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("clipboard.db", AppFolders.DatabaseDefault());
        Assert.EndsWith("actions.json", AppFolders.ActionsDefault());
    }

    [Fact]
    public void Diagnostics_CollectsVersionsAndFolderShortcuts()
    {
        var settings = AppSettings.Default();
        var info = DiagnosticsInfo.Collect(settings);

        Assert.False(string.IsNullOrWhiteSpace(info.AppVersion));
        Assert.False(string.IsNullOrWhiteSpace(info.DotNetVersion));
        Assert.False(string.IsNullOrWhiteSpace(info.SqliteVersion));
        Assert.Equal(AppFolders.DataDir(), info.DataDir);
        Assert.Equal(AppFolders.ConfigDir(), info.ConfigDir);
        Assert.Equal(AppFolders.CacheDir(), info.CacheDir);
        Assert.Equal(settings.History.ResolveDatabasePath(), info.DatabasePath);
        Assert.Equal(AppFolders.ActionsDefault(), info.ActionsPath);
        Assert.Equal(AppFolders.SettingsPath(), info.SettingsPath);
    }
}
