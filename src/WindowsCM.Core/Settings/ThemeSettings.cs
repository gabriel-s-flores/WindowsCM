// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

public enum ThemeChoice
{
    Default = 0,
    // Yaru deliberately absent (GNOME-only cut, grilling 06 Q8).
    Custom = 2,
}

public enum ColorScheme
{
    System = 0,
    Dark = 1,
    Light = 2,
    HighContrast = 3,
}

public enum CustomColorScheme
{
    Dark = 0,
    Light = 1,
}

// Copyous DefaultColors fallbacks ([dark, light] pairs, 01 §1).
public static class CustomThemeDefaults
{
    public const string BgDark = "rgb(54,54,58)";
    public const string BgLight = "rgb(250,250,251)";
    public const string FgDark = "rgb(255,255,255)";
    public const string FgLight = "rgb(34,34,38)";
    public const string CardBgDark = "rgb(71,71,76)";
    public const string CardBgLight = "rgb(255,255,255)";
    public const string SearchBgDark = "rgb(71,71,76)";
    public const string SearchBgLight = "rgb(255,255,255)";
}

public sealed record EffectiveThemeColors(string Bg, string Fg, string CardBg, string SearchBg);

// Theme screen (Copyous Theme parity minus Yaru): Default/Custom,
// system-follow default, four custom colors over the Copyous fallbacks.
// The settings window re-resolves on every change so colors apply live;
// Core owns the resolution so it is test-pinned.
public sealed class ThemeSettings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.Default;
    public ColorScheme Scheme { get; set; } = ColorScheme.System;
    public CustomColorScheme CustomScheme { get; set; } = CustomColorScheme.Dark;
    public string CustomBg { get; set; } = "";
    public string CustomFg { get; set; } = "";
    public string CustomCardBg { get; set; } = "";
    public string CustomSearchBg { get; set; } = "";

    // System resolves through the OS-followed scheme the UI passes in
    // (dark when the host gives no answer). HighContrast has no custom
    // pair upstream, so it falls back to the dark set.
    public EffectiveThemeColors ResolveEffective(ColorScheme systemScheme = ColorScheme.Dark)
    {
        if (Theme == ThemeChoice.Default)
        {
            var resolved = Scheme == ColorScheme.System ? systemScheme : Scheme;
            return resolved switch
            {
                ColorScheme.Light => new EffectiveThemeColors(
                    CustomThemeDefaults.BgLight, CustomThemeDefaults.FgLight,
                    CustomThemeDefaults.CardBgLight, CustomThemeDefaults.SearchBgLight),
                _ => new EffectiveThemeColors(
                    CustomThemeDefaults.BgDark, CustomThemeDefaults.FgDark,
                    CustomThemeDefaults.CardBgDark, CustomThemeDefaults.SearchBgDark),
            };
        }

        var light = CustomScheme == CustomColorScheme.Light;
        return new EffectiveThemeColors(
            OrFallback(CustomBg, light ? CustomThemeDefaults.BgLight : CustomThemeDefaults.BgDark),
            OrFallback(CustomFg, light ? CustomThemeDefaults.FgLight : CustomThemeDefaults.FgDark),
            OrFallback(CustomCardBg, light ? CustomThemeDefaults.CardBgLight : CustomThemeDefaults.CardBgDark),
            OrFallback(CustomSearchBg, light ? CustomThemeDefaults.SearchBgLight : CustomThemeDefaults.SearchBgDark));
    }

    private static string OrFallback(string custom, string fallback) =>
        string.IsNullOrWhiteSpace(custom) ? fallback : custom.Trim();
}
