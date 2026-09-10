// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Release;

namespace WindowsCM.Core.Tests.Release;

// Installer/app contract (ticket 18, grilling 08 Q16): the single source
// of truth installer/WindowsCM.iss must mirror. InstallerScriptTests pin
// the script side; these pin the Core side.
public sealed class InstallerContractTests
{
    [Fact]
    public void AppId_IsAStableGuid()
    {
        Assert.True(Guid.TryParse(InstallerContract.AppId, out _));
    }

    [Fact]
    public void Names_MatchProductAndRuntime()
    {
        Assert.Equal("WindowsCM", InstallerContract.AppName);
        Assert.Equal("WindowsCM.exe", InstallerContract.ExeName);
        Assert.Equal("1.0.0", InstallerContract.AppVersion);
    }

    [Fact]
    public void InstallSubPath_IsPerUserProgramsDir()
    {
        Assert.Equal(@"Programs\WindowsCM", InstallerContract.InstallSubPath);
    }

    [Fact]
    public void MinWindowsBuild_Is20H2Floor()
    {
        Assert.Equal(19042, InstallerContract.MinWindowsBuild);
    }

    [Fact]
    public void Autostart_MatchesAutostartManager()
    {
        Assert.Equal(AutostartManager.RunValueName, InstallerContract.RunValueName);

        var command = InstallerContract.ExpectedAutostartCommand(@"C:\Users\Alba\AppData\Local\Programs\WindowsCM");
        Assert.Equal(AutostartManager.BuildCommand(
            @"C:\Users\Alba\AppData\Local\Programs\WindowsCM\WindowsCM.exe"), command);
        Assert.EndsWith("--hidden", command);
    }
}
