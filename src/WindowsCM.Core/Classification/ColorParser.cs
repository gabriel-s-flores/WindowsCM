// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Classification;

// CSS Color 4 spaces (Copyous `ColorSpace`).
public enum ColorSpace
{
    Rgb,
    Hex,
    Hsl,
    Hwb,
    LinearRgb,
    Xyz,
    Lab,
    Lch,
    Oklab,
    Oklch,
}

// A parsed color. Built through Create so ranges match Copyous clamping.
// Conversions to other spaces are out of scope here; they land with the
// actions engine (issue 13), which reuses this parser.
public sealed record ParsedColor(ColorSpace Space, double C1, double C2, double C3, double Alpha)
{
    public static ParsedColor Create(ColorSpace space, double c1, double c2, double c3, double alpha)
    {
        (c1, c2, c3) = space switch
        {
            ColorSpace.Rgb or ColorSpace.Hex => (Clamp(c1, 0, 255), Clamp(c2, 0, 255), Clamp(c3, 0, 255)),
            ColorSpace.Hsl => (WrapHue(c1), Clamp(c2, 0, 100), Clamp(c3, 0, 100)),
            ColorSpace.Hwb => (WrapHue(c1), Clamp(c2, 0, 100), Clamp(c3, 0, 100)),
            ColorSpace.LinearRgb => (Clamp(c1, 0, 1), Clamp(c2, 0, 1), Clamp(c3, 0, 1)),
            ColorSpace.Xyz => (Math.Max(0, c1), Math.Max(0, c2), Math.Max(0, c3)),
            ColorSpace.Lab => (Clamp(c1, 0, 100), Clamp(c2, -125, 125), Clamp(c3, -125, 125)),
            ColorSpace.Lch => (Clamp(c1, 0, 100), Clamp(c2, 0, 150), WrapHue(c3)),
            ColorSpace.Oklab => (Clamp(c1, 0, 1), Clamp(c2, -0.4, 0.4), Clamp(c3, -0.4, 0.4)),
            ColorSpace.Oklch => (Clamp(c1, 0, 1), Clamp(c2, 0, 0.4), WrapHue(c3)),
            _ => (c1, c2, c3),
        };
        return new ParsedColor(space, c1, c2, c3, Clamp(alpha, 0, 1));
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private static double WrapHue(double hue) => (hue % 360) + (hue < 0 ? 360 : 0);
}

// Color detection: a port of the Copyous CSS Color 4 grammar
// (`color.ts`: named + hex + rgb + hsl + hwb + linear-rgb + xyz + lab +
// lch + oklab + oklch). Only detection lives here; the caller passes
// already-trimmed text and this never trims.
//
// Parity notes: functional notations match case-sensitively (the original
// RegExp has no `i` flag) while named colors ignore case; `none` components
// read as 0 (alpha defaults to 1 when absent). Inputs over 500 chars are
// rejected up front — the longest valid color is under 100 chars, so this
// only skips doomed full-document regex runs.
public static class ColorParser
{
    private const int MaxColorLength = 500;

    private const string NumPattern = @"[+-]?(?:\d*\.\d+|\d+)(?:[eE][+-]?\d+)?";
    private const string AnglePattern = @"(?:(" + NumPattern + @")(deg|grad|rad|turn)?|none)";
    private const string PercentPattern = @"(?:(" + NumPattern + @")(%)?|none)";
    private const string AlphaPattern = @"(?:\s*(?:\/|,|\s)\s*" + PercentPattern + @")?";
    private const string CommaPattern = @"(?:\s+|\s*,\s*)";

    private static readonly Regex RgbRegex = new(
        @"^rgba?\(\s*" + PercentPattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HslRegex = new(
        @"^hsla?\(\s*" + AnglePattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HwbRegex = new(
        @"^hwb\(\s*" + AnglePattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LinearRgbRegex = new(
        @"^color\(\s*srgb-linear" + CommaPattern + PercentPattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XyzRegex = new(
        @"^color\(\s*xyz" + CommaPattern + PercentPattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LabRegex = new(
        @"^lab\(\s*" + PercentPattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LchRegex = new(
        @"^lch\(\s*" + PercentPattern + CommaPattern + PercentPattern + CommaPattern + AnglePattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex OklabRegex = new(
        @"^oklab\(\s*" + PercentPattern + CommaPattern + PercentPattern + CommaPattern + PercentPattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex OklchRegex = new(
        @"^oklch\(\s*" + PercentPattern + CommaPattern + PercentPattern + CommaPattern + AnglePattern + AlphaPattern + @"\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HexRegex = new(
        @"^#([0-9a-fA-F]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // CSS named colors (color-name index pinned by Copyous).
    private static readonly Dictionary<string, (double R, double G, double B)> NamedColors =
        new(StringComparer.OrdinalIgnoreCase)
        {
        ["aliceblue"] = (240, 248, 255),
        ["antiquewhite"] = (250, 235, 215),
        ["aqua"] = (0, 255, 255),
        ["aquamarine"] = (127, 255, 212),
        ["azure"] = (240, 255, 255),
        ["beige"] = (245, 245, 220),
        ["bisque"] = (255, 228, 196),
        ["black"] = (0, 0, 0),
        ["blanchedalmond"] = (255, 235, 205),
        ["blue"] = (0, 0, 255),
        ["blueviolet"] = (138, 43, 226),
        ["brown"] = (165, 42, 42),
        ["burlywood"] = (222, 184, 135),
        ["cadetblue"] = (95, 158, 160),
        ["chartreuse"] = (127, 255, 0),
        ["chocolate"] = (210, 105, 30),
        ["coral"] = (255, 127, 80),
        ["cornflowerblue"] = (100, 149, 237),
        ["cornsilk"] = (255, 248, 220),
        ["crimson"] = (220, 20, 60),
        ["cyan"] = (0, 255, 255),
        ["darkblue"] = (0, 0, 139),
        ["darkcyan"] = (0, 139, 139),
        ["darkgoldenrod"] = (184, 134, 11),
        ["darkgray"] = (169, 169, 169),
        ["darkgreen"] = (0, 100, 0),
        ["darkgrey"] = (169, 169, 169),
        ["darkkhaki"] = (189, 183, 107),
        ["darkmagenta"] = (139, 0, 139),
        ["darkolivegreen"] = (85, 107, 47),
        ["darkorange"] = (255, 140, 0),
        ["darkorchid"] = (153, 50, 204),
        ["darkred"] = (139, 0, 0),
        ["darksalmon"] = (233, 150, 122),
        ["darkseagreen"] = (143, 188, 143),
        ["darkslateblue"] = (72, 61, 139),
        ["darkslategray"] = (47, 79, 79),
        ["darkslategrey"] = (47, 79, 79),
        ["darkturquoise"] = (0, 206, 209),
        ["darkviolet"] = (148, 0, 211),
        ["deeppink"] = (255, 20, 147),
        ["deepskyblue"] = (0, 191, 255),
        ["dimgray"] = (105, 105, 105),
        ["dimgrey"] = (105, 105, 105),
        ["dodgerblue"] = (30, 144, 255),
        ["firebrick"] = (178, 34, 34),
        ["floralwhite"] = (255, 250, 240),
        ["forestgreen"] = (34, 139, 34),
        ["fuchsia"] = (255, 0, 255),
        ["gainsboro"] = (220, 220, 220),
        ["ghostwhite"] = (248, 248, 255),
        ["gold"] = (255, 215, 0),
        ["goldenrod"] = (218, 165, 32),
        ["gray"] = (128, 128, 128),
        ["green"] = (0, 128, 0),
        ["greenyellow"] = (173, 255, 47),
        ["grey"] = (128, 128, 128),
        ["honeydew"] = (240, 255, 240),
        ["hotpink"] = (255, 105, 180),
        ["indianred"] = (205, 92, 92),
        ["indigo"] = (75, 0, 130),
        ["ivory"] = (255, 255, 240),
        ["khaki"] = (240, 230, 140),
        ["lavender"] = (230, 230, 250),
        ["lavenderblush"] = (255, 240, 245),
        ["lawngreen"] = (124, 252, 0),
        ["lemonchiffon"] = (255, 250, 205),
        ["lightblue"] = (173, 216, 230),
        ["lightcoral"] = (240, 128, 128),
        ["lightcyan"] = (224, 255, 255),
        ["lightgoldenrodyellow"] = (250, 250, 210),
        ["lightgray"] = (211, 211, 211),
        ["lightgreen"] = (144, 238, 144),
        ["lightgrey"] = (211, 211, 211),
        ["lightpink"] = (255, 182, 193),
        ["lightsalmon"] = (255, 160, 122),
        ["lightseagreen"] = (32, 178, 170),
        ["lightskyblue"] = (135, 206, 250),
        ["lightslategray"] = (119, 136, 153),
        ["lightslategrey"] = (119, 136, 153),
        ["lightsteelblue"] = (176, 196, 222),
        ["lightyellow"] = (255, 255, 224),
        ["lime"] = (0, 255, 0),
        ["limegreen"] = (50, 205, 50),
        ["linen"] = (250, 240, 230),
        ["magenta"] = (255, 0, 255),
        ["maroon"] = (128, 0, 0),
        ["mediumaquamarine"] = (102, 205, 170),
        ["mediumblue"] = (0, 0, 205),
        ["mediumorchid"] = (186, 85, 211),
        ["mediumpurple"] = (147, 112, 219),
        ["mediumseagreen"] = (60, 179, 113),
        ["mediumslateblue"] = (123, 104, 238),
        ["mediumspringgreen"] = (0, 250, 154),
        ["mediumturquoise"] = (72, 209, 204),
        ["mediumvioletred"] = (199, 21, 133),
        ["midnightblue"] = (25, 25, 112),
        ["mintcream"] = (245, 255, 250),
        ["mistyrose"] = (255, 228, 225),
        ["moccasin"] = (255, 228, 181),
        ["navajowhite"] = (255, 222, 173),
        ["navy"] = (0, 0, 128),
        ["oldlace"] = (253, 245, 230),
        ["olive"] = (128, 128, 0),
        ["olivedrab"] = (107, 142, 35),
        ["orange"] = (255, 165, 0),
        ["orangered"] = (255, 69, 0),
        ["orchid"] = (218, 112, 214),
        ["palegoldenrod"] = (238, 232, 170),
        ["palegreen"] = (152, 251, 152),
        ["paleturquoise"] = (175, 238, 238),
        ["palevioletred"] = (219, 112, 147),
        ["papayawhip"] = (255, 239, 213),
        ["peachpuff"] = (255, 218, 185),
        ["peru"] = (205, 133, 63),
        ["pink"] = (255, 192, 203),
        ["plum"] = (221, 160, 221),
        ["powderblue"] = (176, 224, 230),
        ["purple"] = (128, 0, 128),
        ["rebeccapurple"] = (102, 51, 153),
        ["red"] = (255, 0, 0),
        ["rosybrown"] = (188, 143, 143),
        ["royalblue"] = (65, 105, 225),
        ["saddlebrown"] = (139, 69, 19),
        ["salmon"] = (250, 128, 114),
        ["sandybrown"] = (244, 164, 96),
        ["seagreen"] = (46, 139, 87),
        ["seashell"] = (255, 245, 238),
        ["sienna"] = (160, 82, 45),
        ["silver"] = (192, 192, 192),
        ["skyblue"] = (135, 206, 235),
        ["slateblue"] = (106, 90, 205),
        ["slategray"] = (112, 128, 144),
        ["slategrey"] = (112, 128, 144),
        ["snow"] = (255, 250, 250),
        ["springgreen"] = (0, 255, 127),
        ["steelblue"] = (70, 130, 180),
        ["tan"] = (210, 180, 140),
        ["teal"] = (0, 128, 128),
        ["thistle"] = (216, 191, 216),
        ["tomato"] = (255, 99, 71),
        ["turquoise"] = (64, 224, 208),
        ["violet"] = (238, 130, 238),
        ["wheat"] = (245, 222, 179),
        ["white"] = (255, 255, 255),
        ["whitesmoke"] = (245, 245, 245),
        ["yellow"] = (255, 255, 0),
        ["yellowgreen"] = (154, 205, 50),
        };

    public static bool IsColor(string? text) => TryParse(text) is not null;

    public static ParsedColor? TryParse(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxColorLength)
        {
            return null;
        }
        return ParseNamed(text)
            ?? ParseHex(text)
            ?? ParseFunctional(RgbRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Rgb, Percent(p[0], p[1], 255), Percent(p[2], p[3], 255), Percent(p[4], p[5], 255), a))
            ?? ParseFunctional(HslRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Hsl, Angle(p[0], p[1]), Percent(p[2], p[3], 100), Percent(p[4], p[5], 100), a))
            ?? ParseFunctional(HwbRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Hwb, Angle(p[0], p[1]), Percent(p[2], p[3], 100), Percent(p[4], p[5], 100), a))
            ?? ParseFunctional(LinearRgbRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.LinearRgb, Percent(p[0], p[1], 1), Percent(p[2], p[3], 1), Percent(p[4], p[5], 1), a))
            ?? ParseFunctional(XyzRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Xyz, Percent(p[0], p[1], 1), Percent(p[2], p[3], 1), Percent(p[4], p[5], 1), a))
            ?? ParseFunctional(LabRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Lab, Percent(p[0], p[1], 100), Percent(p[2], p[3], 125), Percent(p[4], p[5], 125), a))
            ?? ParseFunctional(LchRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Lch, Percent(p[0], p[1], 100), Percent(p[2], p[3], 150), Angle(p[4], p[5]), a))
            ?? ParseFunctional(OklabRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Oklab, Percent(p[0], p[1], 1), Percent(p[2], p[3], 0.4), Percent(p[4], p[5], 0.4), a))
            ?? ParseFunctional(OklchRegex, text, (p, a) => ParsedColor.Create(
                ColorSpace.Oklch, Percent(p[0], p[1], 1), Percent(p[2], p[3], 0.4), Angle(p[4], p[5]), a));
    }

    private static ParsedColor? ParseNamed(string text)
    {
        if (!NamedColors.TryGetValue(text, out var rgb))
        {
            return null;
        }
        return ParsedColor.Create(ColorSpace.Rgb, rgb.R, rgb.G, rgb.B, 1);
    }

    private static ParsedColor? ParseHex(string text)
    {
        var match = HexRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }
        var hex = match.Groups[1].Value;
        int[] rgba;
        if (hex.Length is 3 or 4)
        {
            rgba = hex.Select(c => Convert.ToInt32(new string(c, 2), 16)).ToArray();
        }
        else if (hex.Length is 6 or 8)
        {
            rgba = Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToInt32(hex.Substring(i * 2, 2), 16))
                .ToArray();
        }
        else
        {
            return null;
        }
        // Deviation: a zero alpha nibble maps to 0. The original
        // `rgba[3] ? rgba[3] / 255 : 1` reads 0 as missing (JS falsiness)
        // and reports opaque; transparent black is transparent here.
        return ParsedColor.Create(
            ColorSpace.Hex, rgba[0], rgba[1], rgba[2],
            rgba.Length > 3 ? rgba[3] / 255.0 : 1);
    }

    // Groups 1-6 carry the three components (number, unit each); groups 7-8
    // carry alpha. Unmatched groups read as missing, exactly like JS
    // `undefined` captures.
    private static ParsedColor? ParseFunctional(
        Regex regex, string text, Func<string?[], double, ParsedColor> build)
    {
        var match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }
        string?[] parts = Enumerable.Range(1, 6)
            .Select(i => match.Groups[i].Success && match.Groups[i].Value.Length != 0
                ? match.Groups[i].Value
                : null)
            .ToArray();
        double alpha;
        try
        {
            alpha = AlphaValue(GroupOrNull(match, 7), GroupOrNull(match, 8));
            return build(parts, alpha);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static string? GroupOrNull(Match match, int index) =>
        match.Groups[index].Success && match.Groups[index].Value.Length != 0
            ? match.Groups[index].Value
            : null;

    private static double Number(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static double Angle(string? number, string? unit)
    {
        if (number is null)
        {
            return 0;
        }
        var n = Number(number);
        return unit switch
        {
            "deg" => n,
            "grad" => n / 400 * 360,
            "rad" => n / Math.PI * 180,
            "turn" => n * 360,
            _ => n,
        };
    }

    private static double Percent(string? number, string? unit, double max)
    {
        if (number is null)
        {
            return 0;
        }
        var n = Number(number);
        return unit is not null ? n / 100 * max : n;
    }

    private static double AlphaValue(string? number, string? unit)
    {
        if (number is null)
        {
            return 1;
        }
        var n = Number(number);
        return unit is not null ? n / 100 : n;
    }
}

