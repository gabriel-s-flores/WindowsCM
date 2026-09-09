// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Actions;

// Color conversions reusing the ported parser (Copyous `Color.toColor`
// and `toString` parity, https://www.w3.org/TR/css-color-4/#color-conversion-code).
// The graph routes through hub spaces exactly like the original:
//
//   rgb ⇄ hsl, rgb ⇄ hwb, rgb ⇄ hex, rgb ⇄ linear ⇄ xyz ⇄ lab ⇄ lch
//                                                      xyz ⇄ oklab ⇄ oklch
//
// Every step constructs through ParsedColor.Create, so the Copyous
// clamping (rgb 0–255, hue wrap, lab ranges…) holds on every conversion.
// Pure: parse with ColorParser, convert here, format with Format.
public static class ColorConverter
{
    private static readonly double D50X = 0.3457 / 0.3585;
    private static readonly double D50Z = (1.0 - 0.3457 - 0.3585) / 0.3585;

    public static ParsedColor Convert(ParsedColor color, ColorSpace target) => target switch
    {
        ColorSpace.Rgb => ToRgb(color),
        ColorSpace.Hex => ToHex(color),
        ColorSpace.Hsl => ToHsl(color),
        ColorSpace.Hwb => ToHwb(color),
        ColorSpace.LinearRgb => ToLinear(color),
        ColorSpace.Xyz => ToXyz(color),
        ColorSpace.Lab => ToLab(color),
        ColorSpace.Lch => ToLch(color),
        ColorSpace.Oklab => ToOklab(color),
        ColorSpace.Oklch => ToOklch(color),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    // Copyous `toString` parity: digit counts per space (xyz 4, linear/lab
    // family 2, screen spaces 0), alpha as " / x" only when not opaque,
    // lowercase padded hex.
    public static string Format(ParsedColor color)
    {
        var digits = color.Space == ColorSpace.Xyz ? 4
            : color.Space is ColorSpace.LinearRgb or ColorSpace.Lab
                or ColorSpace.Lch or ColorSpace.Oklab or ColorSpace.Oklch ? 2 : 0;
        var c1 = RoundDigits(color.C1, digits);
        var c2 = RoundDigits(color.C2, digits);
        var c3 = RoundDigits(color.C3, digits);
        // Invariant numerals: this string is pasted into apps and reparsed
        // by the grammar, so a comma-decimal locale must never leak in.
        static string Num(double value) =>
            value.ToString(CultureInfo.InvariantCulture);
        var alpha = color.Alpha == 1 ? string.Empty : $" / {Num(RoundDigits(color.Alpha, 2))}";
        return color.Space switch
        {
            ColorSpace.Rgb => $"rgb({Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.Hex => $"#{Hex(c1)}{Hex(c2)}{Hex(c3)}"
                + (color.Alpha == 1 ? string.Empty : Hex(color.Alpha * 255)),
            ColorSpace.Hsl => $"hsl({Num(c1)} {Num(c2)}% {Num(c3)}%{alpha})",
            ColorSpace.Hwb => $"hwb({Num(c1)} {Num(c2)}% {Num(c3)}%{alpha})",
            ColorSpace.Lab => $"lab({Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.Lch => $"lch({Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.Oklab => $"oklab({Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.Oklch => $"oklch({Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.LinearRgb => $"color(srgb-linear {Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            ColorSpace.Xyz => $"color(xyz {Num(c1)} {Num(c2)} {Num(c3)}{alpha})",
            _ => throw new ArgumentOutOfRangeException(nameof(color)),
        };
    }

    private static double RoundDigits(double value, int digits) =>
        double.Parse(
            value.ToString("F" + digits, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

    private static string Hex(double channel) =>
        System.Convert.ToString(
            (int)Math.Round(channel, MidpointRounding.AwayFromZero), 16).PadLeft(2, '0');

    private static ParsedColor ToRgb(ParsedColor color) => color.Space switch
    {
        ColorSpace.Rgb => color,
        ColorSpace.Hex => ParsedColor.Create(
            ColorSpace.Rgb, color.C1, color.C2, color.C3, color.Alpha),
        ColorSpace.Hsl => HslToRgb(color),
        ColorSpace.Hwb => HwbToRgb(color),
        _ => LinearToRgb(ToLinear(color)),
    };

    private static ParsedColor ToHex(ParsedColor color) =>
        color.Space == ColorSpace.Hex
            ? color
            : ParsedColor.Create(
                ColorSpace.Hex, ToRgb(color).C1, ToRgb(color).C2, ToRgb(color).C3, color.Alpha);

    private static ParsedColor ToHsl(ParsedColor color) =>
        color.Space == ColorSpace.Hsl ? color : RgbToHsl(ToRgb(color));

    private static ParsedColor ToHwb(ParsedColor color) =>
        color.Space == ColorSpace.Hwb ? color : RgbToHwb(ToRgb(color));

    private static ParsedColor ToLinear(ParsedColor color) => color.Space switch
    {
        ColorSpace.LinearRgb => color,
        ColorSpace.Rgb or ColorSpace.Hex or ColorSpace.Hsl or ColorSpace.Hwb =>
            RgbToLinear(ToRgb(color)),
        _ => XyzToLinear(ToXyz(color)),
    };

    private static ParsedColor ToXyz(ParsedColor color) => color.Space switch
    {
        ColorSpace.Xyz => color,
        ColorSpace.Rgb or ColorSpace.Hex or ColorSpace.Hsl
            or ColorSpace.Hwb or ColorSpace.LinearRgb =>
            LinearToXyz(ToLinear(color)),
        ColorSpace.Lab or ColorSpace.Lch => LabToXyz(ToLab(color)),
        ColorSpace.Oklab or ColorSpace.Oklch => OklabToXyz(ToOklab(color)),
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    private static ParsedColor ToLab(ParsedColor color) => color.Space switch
    {
        ColorSpace.Lab => color,
        ColorSpace.Lch => LchToLab(color),
        _ => XyzToLab(ToXyz(color)),
    };

    private static ParsedColor ToLch(ParsedColor color) =>
        color.Space == ColorSpace.Lch ? color : LabToLch(ToLab(color));

    private static ParsedColor ToOklab(ParsedColor color) => color.Space switch
    {
        ColorSpace.Oklab => color,
        ColorSpace.Oklch => OklchToOklab(color),
        _ => XyzToOklab(ToXyz(color)),
    };

    private static ParsedColor ToOklch(ParsedColor color) =>
        color.Space == ColorSpace.Oklch ? color : OklabToOklch(ToOklab(color));

    private static ParsedColor RgbToHsl(ParsedColor rgb)
    {
        var (r, g, b) = (rgb.C1 / 255, rgb.C2 / 255, rgb.C3 / 255);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var light = (max + min) / 2;
        var saturation = light is 0 or 1 ? 0 : (max - light) / Math.Min(light, 1 - light);
        return ParsedColor.Create(
            ColorSpace.Hsl, CalculateHue(rgb), saturation * 100, light * 100, rgb.Alpha);
    }

    private static ParsedColor HslToRgb(ParsedColor hsl)
    {
        var (h, s, l) = (hsl.C1, hsl.C2 / 100, hsl.C3 / 100);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var sector = (h / 60) % 6;
        var x = c * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) =
            sector is >= 0 and < 1 ? (c, x, 0.0)
            : sector is >= 1 and < 2 ? (x, c, 0.0)
            : sector is >= 2 and < 3 ? (0.0, c, x)
            : sector is >= 3 and < 4 ? (0.0, x, c)
            : sector is >= 4 and < 5 ? (x, 0.0, c)
            : (c, 0.0, x);
        var m = l - c / 2;
        return ParsedColor.Create(
            ColorSpace.Rgb, (r + m) * 255, (g + m) * 255, (b + m) * 255, hsl.Alpha);
    }

    private static ParsedColor RgbToHwb(ParsedColor rgb)
    {
        const double Epsilon = 0.00001;
        var (r, g, b) = (rgb.C1 / 255, rgb.C2 / 255, rgb.C3 / 255);
        var hue = CalculateHue(rgb);
        var white = Math.Min(r, Math.Min(g, b));
        var black = 1 - Math.Max(r, Math.Max(g, b));
        if (white + black >= 1 - Epsilon)
        {
            hue = 0;
        }
        return ParsedColor.Create(ColorSpace.Hwb, hue, white * 100, black * 100, rgb.Alpha);
    }

    private static ParsedColor HwbToRgb(ParsedColor hwb)
    {
        var (h, white, black) = (hwb.C1, hwb.C2 / 100, hwb.C3 / 100);
        if (white + black >= 1)
        {
            var gray = white / (white + black);
            return ParsedColor.Create(ColorSpace.Rgb, gray, gray, gray, hwb.Alpha);
        }
        var pure = ToRgb(ParsedColor.Create(ColorSpace.Hsl, h, 100, 50, hwb.Alpha));
        var mix = 1 - white - black;
        var offset = white * 255;
        return ParsedColor.Create(
            ColorSpace.Rgb,
            pure.C1 * mix + offset, pure.C2 * mix + offset, pure.C3 * mix + offset,
            hwb.Alpha);
    }

    private static ParsedColor RgbToLinear(ParsedColor rgb)
    {
        static double Channel(double c) =>
            c > 0.04045 ? Math.Pow((c + 0.055) / 1.055, 2.4) : c / 12.92;
        return ParsedColor.Create(
            ColorSpace.LinearRgb,
            Channel(rgb.C1 / 255), Channel(rgb.C2 / 255), Channel(rgb.C3 / 255),
            rgb.Alpha);
    }

    private static ParsedColor LinearToRgb(ParsedColor linear)
    {
        static double Channel(double c) =>
            c < 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
        var rgb = ParsedColor.Create(
            ColorSpace.Rgb,
            Channel(linear.C1) * 255, Channel(linear.C2) * 255, Channel(linear.C3) * 255,
            linear.Alpha);
        return rgb;
    }

    private static ParsedColor LinearToXyz(ParsedColor linear)
    {
        var (r, g, b) = (linear.C1, linear.C2, linear.C3);
        return ParsedColor.Create(
            ColorSpace.Xyz,
            (506752.0 / 1228815) * r + (87881.0 / 245763) * g + (12673.0 / 70218) * b,
            (87098.0 / 409605) * r + (175762.0 / 245763) * g + (12673.0 / 175545) * b,
            (7918.0 / 409605) * r + (87881.0 / 737289) * g + (1001167.0 / 1053270) * b,
            linear.Alpha);
    }

    private static ParsedColor XyzToLinear(ParsedColor xyz)
    {
        var (x, y, z) = (xyz.C1, xyz.C2, xyz.C3);
        return ParsedColor.Create(
            ColorSpace.LinearRgb,
            (12831.0 / 3959) * x + (-329.0 / 214) * y + (-1974.0 / 3959) * z,
            (-851781.0 / 878810) * x + (1648619.0 / 878810) * y + (36819.0 / 878810) * z,
            (705.0 / 12673) * x + (-2585.0 / 12673) * y + (705.0 / 667) * z,
            xyz.Alpha);
    }

    private static ParsedColor XyzToLab(ParsedColor xyz)
    {
        const double Epsilon = 216.0 / 24389;
        const double Kappa = 24389.0 / 27;
        static double Channel(double c, double divisor) =>
            (c /= divisor) > Epsilon ? Math.Cbrt(c) : (Kappa * c + 16) / 116;
        var fx = Channel(xyz.C1, D50X);
        var fy = Channel(xyz.C2, 1.0);
        var fz = Channel(xyz.C3, D50Z);
        return ParsedColor.Create(
            ColorSpace.Lab, 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz), xyz.Alpha);
    }

    private static ParsedColor LabToXyz(ParsedColor lab)
    {
        const double Epsilon = 216.0 / 24389;
        const double Kappa = 24389.0 / 27;
        var (l, a, b) = (lab.C1, lab.C2, lab.C3);
        var fy = (l + 16) / 116;
        var fx = fy + a / 500;
        var fz = fy - b / 200;
        static double ToCurve(double f) =>
            Math.Pow(f, 3) > Epsilon ? Math.Pow(f, 3) : (116 * f - 16) / Kappa;
        // The L branch guards near-black exactly like the original.
        var y = l > Kappa * Epsilon ? Math.Pow(fy, 3) : l / Kappa;
        return ParsedColor.Create(
            ColorSpace.Xyz, ToCurve(fx) * D50X, y, ToCurve(fz) * D50Z, lab.Alpha);
    }

    private static ParsedColor LabToLch(ParsedColor lab)
    {
        const double Epsilon = 0.0015;
        var chroma = Math.Sqrt(lab.C2 * lab.C2 + lab.C3 * lab.C3);
        var hue = Degrees(Math.Atan2(lab.C3, lab.C2));
        if (chroma <= Epsilon)
        {
            hue = 0;
        }
        return ParsedColor.Create(ColorSpace.Lch, lab.C1, chroma, hue, lab.Alpha);
    }

    private static ParsedColor LchToLab(ParsedColor lch)
    {
        var radians = lch.C3 * Math.PI / 180;
        return ParsedColor.Create(
            ColorSpace.Lab,
            lch.C1, lch.C2 * Math.Cos(radians), lch.C2 * Math.Sin(radians),
            lch.Alpha);
    }

    private static ParsedColor XyzToOklab(ParsedColor xyz)
    {
        var (x, y, z) = (xyz.C1, xyz.C2, xyz.C3);
        var l = Math.Cbrt(0.819022437996703 * x + 0.3619062600528904 * y - 0.1288737815209879 * z);
        var m = Math.Cbrt(0.0329836539323885 * x + 0.9292868615863434 * y + 0.0361446663506424 * z);
        var s = Math.Cbrt(0.0481771893596242 * x + 0.2642395317527308 * y + 0.6335478284694309 * z);
        return ParsedColor.Create(
            ColorSpace.Oklab,
            0.210454268309314 * l + 0.7936177747023054 * m - 0.0040720430116193 * s,
            1.9779985324311684 * l - 2.4285922420485799 * m + 0.450593709617411 * s,
            0.0259040424655478 * l + 0.7827717124575296 * m - 0.8086757549230774 * s,
            xyz.Alpha);
    }

    private static ParsedColor OklabToXyz(ParsedColor oklab)
    {
        var (l, a, b) = (oklab.C1, oklab.C2, oklab.C3);
        var lmsL = Math.Pow(l + 0.3963377773761749 * a + 0.2158037573099136 * b, 3);
        var lmsM = Math.Pow(l - 0.1055613458156586 * a - 0.0638541728258133 * b, 3);
        var lmsS = Math.Pow(l - 0.0894841775298119 * a - 1.2914855480194092 * b, 3);
        return ParsedColor.Create(
            ColorSpace.Xyz,
            1.2268798758459243 * lmsL - 0.5578149944602171 * lmsM + 0.2813910456659647 * lmsS,
            -0.0405757452148008 * lmsL + 1.112286803280317 * lmsM - 0.0717110580655164 * lmsS,
            -0.0763729366746601 * lmsL - 0.4214933324022432 * lmsM + 1.5869240198367816 * lmsS,
            oklab.Alpha);
    }

    private static ParsedColor OklabToOklch(ParsedColor oklab)
    {
        const double Epsilon = 0.000004;
        var chroma = Math.Sqrt(oklab.C2 * oklab.C2 + oklab.C3 * oklab.C3);
        var hue = Degrees(Math.Atan2(oklab.C3, oklab.C2));
        if (chroma <= Epsilon)
        {
            hue = 0;
        }
        return ParsedColor.Create(ColorSpace.Oklch, oklab.C1, chroma, hue, oklab.Alpha);
    }

    private static ParsedColor OklchToOklab(ParsedColor oklch)
    {
        var radians = oklch.C3 * Math.PI / 180;
        return ParsedColor.Create(
            ColorSpace.Oklab,
            oklch.C1, oklch.C2 * Math.Cos(radians), oklch.C2 * Math.Sin(radians),
            oklch.Alpha);
    }

    private static double Degrees(double radians)
    {
        var degrees = radians * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    private static double CalculateHue(ParsedColor rgb)
    {
        var (r, g, b) = (rgb.C1 / 255, rgb.C2 / 255, rgb.C3 / 255);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        if (delta == 0)
        {
            return 0;
        }
        double hue;
        if (max == r)
        {
            hue = ((g - b) / delta % 6) + (b > g ? 6 : 0);
        }
        else if (max == g)
        {
            hue = (b - r) / delta + 2;
        }
        else
        {
            hue = (r - g) / delta + 4;
        }
        return hue * 60;
    }
}
