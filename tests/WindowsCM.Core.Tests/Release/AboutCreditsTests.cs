// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Release;

namespace WindowsCM.Core.Tests.Release;

// About-page credits + license compliance (ticket 18, spec story 43,
// grilling 08 Q17): GPL-3.0-or-later in product and sources, About
// credits Copyous/Pano and every bundled library with its real license.
public sealed class AboutCreditsTests
{
    [Fact]
    public void App_LicenseIsGpl3OrLater()
    {
        Assert.Equal("GPL-3.0-or-later", AboutCredits.LicenseId);
        Assert.Equal("WindowsCM", AboutCredits.AppName);
        Assert.Equal("LICENSE", AboutCredits.LicenseFileName);
    }

    [Fact]
    public void Upstream_CreditsCopyousAndPano()
    {
        var byName = AboutCredits.Upstream.ToDictionary(u => u.Name);

        Assert.Contains("Copyous", byName);
        Assert.Contains("https://github.com/boerdereinar/copyous", byName["Copyous"].Url);
        Assert.Equal("GPL-3.0-or-later", byName["Copyous"].License);

        Assert.Contains("Pano", byName);
        Assert.Contains("https://github.com/oae/gnome-shell-pano", byName["Pano"].Url);
        Assert.False(string.IsNullOrWhiteSpace(byName["Pano"].License));
    }

    [Fact]
    public void Libraries_EveryEntryHasNameUrlAndLicense()
    {
        Assert.NotEmpty(AboutCredits.Libraries);
        foreach (var lib in AboutCredits.Libraries)
        {
            Assert.False(string.IsNullOrWhiteSpace(lib.Name));
            Assert.False(string.IsNullOrWhiteSpace(lib.Url));
            Assert.False(string.IsNullOrWhiteSpace(lib.License));
        }
    }

    [Fact]
    public void Libraries_ContainTheFourMitDecisionsPlusSqlite()
    {
        var byName = AboutCredits.Libraries.ToDictionary(l => l.Name);

        foreach (var mit in new[] { "QRCoder", "AngleSharp", "AvalonEdit", "H.NotifyIcon.WPF" })
        {
            Assert.Contains(mit, byName);
            Assert.Equal("MIT", byName[mit].License);
        }

        Assert.Contains("Microsoft.Data.Sqlite", byName);
        Assert.Equal("Apache-2.0", byName["Microsoft.Data.Sqlite"].License);
        Assert.False(string.IsNullOrWhiteSpace(byName["Microsoft.Data.Sqlite"].Version));
    }

    [Fact]
    public void License_FileAtRootIsGpl3()
    {
        var text = RepoFiles.Read("LICENSE");

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", text);
        Assert.Contains("Version 3, 29 June 2007", text);
        Assert.Contains("END OF TERMS AND CONDITIONS", text);
    }

    [Fact]
    public void Csproj_LicenseExpressionMatchesAboutCredits()
    {
        var csproj = RepoFiles.Read(Path.Combine("src", "WindowsCM.Core", "WindowsCM.Core.csproj"));

        Assert.Contains(
            "<PackageLicenseExpression>" + AboutCredits.LicenseId + "</PackageLicenseExpression>",
            csproj);
    }
}
