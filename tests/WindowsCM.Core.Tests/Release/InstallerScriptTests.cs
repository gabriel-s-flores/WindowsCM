// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Release;

namespace WindowsCM.Core.Tests.Release;

// The installer script itself is the seam: these pin the ticket-18
// behaviors (per-user no-admin, Start Menu only, opt-in autostart,
// LICENSE, safe uninstall) so the .iss can never silently drift from
// the decisions in grilling 08 Q16.
public sealed class InstallerScriptTests
{
    private static string Script() => RepoFiles.Read(Path.Combine("installer", "WindowsCM.iss"));

    [Fact]
    public void Installs_PerUserWithoutAdmin()
    {
        var iss = Script();

        Assert.Contains("PrivilegesRequired=lowest", iss);
        Assert.Contains("DefaultDirName={localappdata}\\" + InstallerContract.InstallSubPath, iss);
    }

    [Fact]
    public void Shortcuts_StartMenuOnly_NoDesktopIcon()
    {
        var iss = Script();

        Assert.Contains("{group}", iss);
        Assert.DoesNotContain("{userdesktop}", iss);
        Assert.DoesNotContain("{commondesktop}", iss);
        Assert.DoesNotContain("{autodesktop}", iss);
    }

    [Fact]
    public void License_ShownAndBundled()
    {
        var iss = Script();

        Assert.Contains("LicenseFile=", iss);
        Assert.Contains("LICENSE", iss);
    }

    [Fact]
    public void Autostart_OptInViaRunKey()
    {
        var iss = Script();

        Assert.Contains("Name: autostart", iss);
        Assert.Contains("Flags: unchecked", iss);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\Run", iss);
        Assert.Contains("ValueName: " + InstallerContract.RunValueName, iss);
        Assert.Contains("--hidden", iss);
        Assert.Contains("uninsdeletevalue", iss);
    }

    [Fact]
    public void Uninstall_RemovesDataOnlyOnCheckbox()
    {
        var iss = Script();

        Assert.Contains("CreateCustomForm", iss);
        Assert.Contains("TNewCheckBox", iss);
        Assert.Contains("Checked := False", iss);
        Assert.Contains("DelTree", iss);
        Assert.Contains("{localappdata}\\WindowsCM", iss);
        Assert.Contains("{userappdata}\\WindowsCM", iss);
        // Dismissing the prompt (X/Cancel) keeps data AND proceeds:
        // removal needs an explicit OK with the box checked.
        Assert.Contains("(Form.ShowModal() = mrOk) and Check.Checked", iss);
        Assert.Contains("Result := True;", iss);
    }

    [Fact]
    public void Contract_AppIdVersionAndMinVersionMatch()
    {
        var iss = Script();

        // AppId travels through an Inno #define; pin both the define and
        // its single use site so the id can never silently change.
        Assert.Contains("#define AppId \"" + InstallerContract.AppId + "\"", iss);
        Assert.Contains("AppId={{#AppId}", iss);
        Assert.Contains(InstallerContract.AppVersion, iss);
        Assert.Contains("10.0." + InstallerContract.MinWindowsBuild, iss);
    }

    [Fact]
    public void ShipsOnlyTheExe_SoPublishMustBundleNativeLibraries()
    {
        var iss = Script();
        var csproj = RepoFiles.Read(Path.Combine("src", "WindowsCM.App", "WindowsCM.App.csproj"));

        // The installer copies publish\WindowsCM.exe alone; native DLLs
        // left beside it by the publish would be missing after install.
        Assert.Contains("Source: \"publish\\{#AppExe}\"", iss);
        Assert.Contains("<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>", csproj);
    }

    [Fact]
    public void Setup_UsesCustomAppIcon()
    {
        var iss = Script();

        Assert.Contains("SetupIconFile=", iss);
        Assert.Contains("app.ico", iss);
    }
}
