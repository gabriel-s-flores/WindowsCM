// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Theming: Default/Custom (Yaru cut), system-follow default, four custom
// colors over the Copyous fallbacks, resolved live.
public sealed class ThemeSettingsTests
{
    [Fact]
    public void Defaults_DefaultTheme_SystemFollow_DarkCustomBase()
    {
        var theme = new ThemeSettings();

        Assert.Equal(ThemeChoice.Default, theme.Theme);
        Assert.Equal(ColorScheme.System, theme.Scheme);
        Assert.Equal(CustomColorScheme.Dark, theme.CustomScheme);
        Assert.Equal("", theme.CustomBg);
        Assert.Equal("", theme.CustomFg);
        Assert.Equal("", theme.CustomCardBg);
        Assert.Equal("", theme.CustomSearchBg);
    }

    [Fact]
    public void Yaru_IsCut_NoEnumMember()
    {
        var names = Enum.GetNames<ThemeChoice>();

        Assert.DoesNotContain("Yaru", names);
        Assert.Equal(2, names.Length);
    }

    [Fact]
    public void DefaultTheme_IgnoresCustoms_UsesSchemeFallbacks()
    {
        var dark = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.Dark };
        var light = new ThemeSettings
        {
            Theme = ThemeChoice.Default,
            Scheme = ColorScheme.Light,
            CustomBg = "rgb(1,2,3)",
        };

        Assert.Equal(CustomThemeDefaults.BgDark, dark.ResolveEffective().Bg);
        Assert.Equal(CustomThemeDefaults.BgLight, light.ResolveEffective().Bg);
    }

    [Fact]
    public void SystemFollow_UsesHostScheme()
    {
        var theme = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.System };

        Assert.Equal(CustomThemeDefaults.BgLight, theme.ResolveEffective(ColorScheme.Light).Bg);
        Assert.Equal(CustomThemeDefaults.BgDark, theme.ResolveEffective(ColorScheme.Dark).Bg);
    }

    [Fact]
    public void CustomTheme_EmptyMeansFallbackPerCustomScheme()
    {
        var dark = new ThemeSettings { Theme = ThemeChoice.Custom, CustomScheme = CustomColorScheme.Dark };
        var light = new ThemeSettings { Theme = ThemeChoice.Custom, CustomScheme = CustomColorScheme.Light };

        var darkColors = dark.ResolveEffective();
        var lightColors = light.ResolveEffective();

        Assert.Equal(CustomThemeDefaults.BgDark, darkColors.Bg);
        Assert.Equal(CustomThemeDefaults.FgDark, darkColors.Fg);
        Assert.Equal(CustomThemeDefaults.CardBgDark, darkColors.CardBg);
        Assert.Equal(CustomThemeDefaults.SearchBgDark, darkColors.SearchBg);
        Assert.Equal(CustomThemeDefaults.BgLight, lightColors.Bg);
        Assert.Equal(CustomThemeDefaults.FgLight, lightColors.Fg);
    }

    [Fact]
    public void CustomTheme_NonEmptyOverridesFallbackLive()
    {
        var theme = new ThemeSettings
        {
            Theme = ThemeChoice.Custom,
            CustomScheme = CustomColorScheme.Dark,
            CustomBg = "rgb(10,20,30)",
        };

        Assert.Equal("rgb(10,20,30)", theme.ResolveEffective().Bg);
        Assert.Equal(CustomThemeDefaults.FgDark, theme.ResolveEffective().Fg);

        theme.CustomFg = "  rgb(40,50,60)  ";
        Assert.Equal("rgb(40,50,60)", theme.ResolveEffective().Fg);
    }

    [Fact]
    public void HighContrast_FallsBackToDarkSet()
    {
        var theme = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.HighContrast };

        Assert.Equal(CustomThemeDefaults.BgDark, theme.ResolveEffective().Bg);
    }
}
