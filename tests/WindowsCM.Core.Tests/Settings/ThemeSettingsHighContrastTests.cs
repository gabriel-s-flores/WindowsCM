// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

public sealed class ThemeSettingsHighContrastTests
{
    [Fact]
    public void ResolveEffective_HighContrast_ReturnsPureBlackBackgroundAndWhiteForeground()
    {
        var settings = new ThemeSettings
        {
            Theme = ThemeChoice.Default,
            Scheme = ColorScheme.HighContrast,
        };

        var colors = settings.ResolveEffective();

        Assert.Equal("rgb(0,0,0)", colors.Bg);
        Assert.Equal("rgb(255,255,255)", colors.Fg);
        Assert.Equal("rgb(0,0,0)", colors.CardBg);
        Assert.Equal("rgb(0,0,0)", colors.SearchBg);
    }

    [Fact]
    public void ResolveEffective_SystemWithHighContrast_ResolvesHighContrast()
    {
        var settings = new ThemeSettings
        {
            Theme = ThemeChoice.Default,
            Scheme = ColorScheme.System,
        };

        var colors = settings.ResolveEffective(ColorScheme.HighContrast);

        Assert.Equal("rgb(0,0,0)", colors.Bg);
        Assert.Equal("rgb(255,255,255)", colors.Fg);
    }

    [Theory]
    [InlineData(ColorScheme.Dark, "rgb(54,54,58)", "rgb(255,255,255)")]
    [InlineData(ColorScheme.Light, "rgb(250,250,251)", "rgb(34,34,38)")]
    [InlineData(ColorScheme.HighContrast, "rgb(0,0,0)", "rgb(255,255,255)")]
    public void ResolveEffective_AllSchemes_ProducesDistinctAccessiblePairs(
        ColorScheme scheme, string expectedBg, string expectedFg)
    {
        var settings = new ThemeSettings
        {
            Theme = ThemeChoice.Default,
            Scheme = scheme,
        };

        var colors = settings.ResolveEffective();

        Assert.Equal(expectedBg, colors.Bg);
        Assert.Equal(expectedFg, colors.Fg);
    }
}
