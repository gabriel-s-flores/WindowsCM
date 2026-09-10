// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Release;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Release;

// Upgrade/uninstall data safety (ticket 18, grilling 08 Q16): upgrades
// preserve user data, uninstall keeps it unless the removal checkbox is
// set. The structural guarantee is disjointness — both data roots live
// outside the install root, so replacing or deleting {app} cannot reach
// history, settings, actions, images or caches.
public sealed class UserDataPolicyTests
{
    [Fact]
    public void PreservedPaths_CoverDatabaseSettingsActionsImagesAndCache()
    {
        var preserved = UserDataPolicy.PreservedPaths();

        Assert.Contains(AppFolders.DatabaseDefault(), preserved);
        Assert.Contains(AppFolders.SettingsPath(), preserved);
        Assert.Contains(AppFolders.ActionsDefault(), preserved);
        Assert.Contains(AppFolders.ImagesDir(), preserved);
        Assert.Contains(AppFolders.CacheDir(), preserved);
    }

    [Fact]
    public void PreservedPaths_AreAllClassifiedAsUserData()
    {
        foreach (var path in UserDataPolicy.PreservedPaths())
        {
            Assert.True(UserDataPolicy.IsUserData(path), path);
        }
    }

    [Fact]
    public void InstallDir_ContainsNoUserData()
    {
        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallerContract.InstallSubPath);
        var exe = Path.Combine(installDir, InstallerContract.ExeName);

        Assert.False(UserDataPolicy.IsUserData(exe));
        Assert.False(UserDataPolicy.IsUnderInstallRoot(UserDataPolicy.DataRoot(), installDir));
        Assert.False(UserDataPolicy.IsUnderInstallRoot(UserDataPolicy.ConfigRoot(), installDir));
        Assert.True(UserDataPolicy.IsUnderInstallRoot(exe, installDir));
    }

    [Fact]
    public void ShouldDeleteUserData_OnlyOnRemoveChoice()
    {
        Assert.False(UserDataPolicy.ShouldDeleteUserData(UninstallDataChoice.KeepUserData));
        Assert.True(UserDataPolicy.ShouldDeleteUserData(UninstallDataChoice.RemoveUserData));
    }

    [Fact]
    public void IsUserData_DoesNotMatchSiblingPrefix()
    {
        var sibling = UserDataPolicy.DataRoot() + "2";

        Assert.False(UserDataPolicy.IsUserData(sibling));
    }
}
