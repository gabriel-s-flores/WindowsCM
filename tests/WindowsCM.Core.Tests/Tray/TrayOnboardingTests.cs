// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Tests.Tray;

public sealed class TrayOnboardingTests
{
    [Fact]
    public void Guidance_IsNonEmpty_AndMentionsWindows11AndOverflow()
    {
        Assert.False(string.IsNullOrWhiteSpace(TrayOnboarding.Guidance));
        Assert.Contains("Windows 11", TrayOnboarding.Guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("overflow", TrayOnboarding.Guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("drag", TrayOnboarding.Guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guidance_ExplicitlyDisclaimsProgrammaticPromotion()
    {
        Assert.Contains("promotion", TrayOnboarding.Guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuidanceSummary_IsShortAndAccurate()
    {
        Assert.False(string.IsNullOrWhiteSpace(TrayOnboarding.GuidanceSummary));
        Assert.Contains("drag", TrayOnboarding.GuidanceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("overflow", TrayOnboarding.GuidanceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(TrayOnboarding.GuidanceSummary.Length < 120);
    }

    [Fact]
    public void Diagnostics_IncludesTrayGuidanceByDefault()
    {
        var info = DiagnosticsInfo.Collect();
        Assert.Equal(TrayOnboarding.Guidance, info.TrayGuidance);
    }
}
