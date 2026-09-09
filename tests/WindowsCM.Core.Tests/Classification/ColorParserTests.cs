// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class ColorParserTests
{
    [Theory]
    [InlineData("red", ColorSpace.Rgb, 255, 0, 0, 1)]
    [InlineData("rebeccapurple", ColorSpace.Rgb, 102, 51, 153, 1)]
    [InlineData("#fff", ColorSpace.Hex, 255, 255, 255, 1)]
    [InlineData("#FFFFFF", ColorSpace.Hex, 255, 255, 255, 1)]
    [InlineData("#ff0000", ColorSpace.Hex, 255, 0, 0, 1)]
    [InlineData("rgb(255 0 0)", ColorSpace.Rgb, 255, 0, 0, 1)]
    [InlineData("rgb(255, 0, 0)", ColorSpace.Rgb, 255, 0, 0, 1)]
    [InlineData("rgb(100% 0% 0%)", ColorSpace.Rgb, 255, 0, 0, 1)]
    [InlineData("rgb(none none none)", ColorSpace.Rgb, 0, 0, 0, 1)]
    [InlineData("rgba(0, 0, 0, 0.5)", ColorSpace.Rgb, 0, 0, 0, 0.5)]
    [InlineData("rgb(0 0 0 / 50%)", ColorSpace.Rgb, 0, 0, 0, 0.5)]
    [InlineData("hsl(0 100% 50%)", ColorSpace.Hsl, 0, 100, 50, 1)]
    [InlineData("hsla(120, 50%, 50%, 0.5)", ColorSpace.Hsl, 120, 50, 50, 0.5)]
    [InlineData("hwb(0 0% 0%)", ColorSpace.Hwb, 0, 0, 0, 1)]
    [InlineData("color(srgb-linear 1 0 0)", ColorSpace.LinearRgb, 1, 0, 0, 1)]
    [InlineData("color(xyz 0.2 0.2 0.2)", ColorSpace.Xyz, 0.2, 0.2, 0.2, 1)]
    [InlineData("lab(50 20 30)", ColorSpace.Lab, 50, 20, 30, 1)]
    [InlineData("lch(50 20 180)", ColorSpace.Lch, 50, 20, 180, 1)]
    [InlineData("oklab(0.5 0.05 -0.05)", ColorSpace.Oklab, 0.5, 0.05, -0.05, 1)]
    [InlineData("oklch(0.5 0.05 180)", ColorSpace.Oklch, 0.5, 0.05, 180, 1)]
    public void TryParse_ValidColors_ReturnComponents(
        string text, ColorSpace space, double c1, double c2, double c3, double alpha)
    {
        var parsed = ColorParser.TryParse(text);

        Assert.NotNull(parsed);
        Assert.Equal(space, parsed.Space);
        Assert.Equal(c1, parsed.C1);
        Assert.Equal(c2, parsed.C2);
        Assert.Equal(c3, parsed.C3);
        Assert.Equal(alpha, parsed.Alpha);
    }

    [Fact]
    public void TryParse_NamedColor_IgnoresCase()
    {
        Assert.NotNull(ColorParser.TryParse("Red"));
    }

    [Fact]
    public void TryParse_ShortHexWithAlpha_MapsAlpha()
    {
        var parsed = ColorParser.TryParse("#0008");

        Assert.NotNull(parsed);
        Assert.Equal(0, parsed.C1);
        Assert.Equal(136 / 255.0, parsed.Alpha);
    }

    [Fact]
    public void TryParse_LongHexWithAlpha_MapsAlpha()
    {
        var parsed = ColorParser.TryParse("#ff000080");

        Assert.NotNull(parsed);
        Assert.Equal(255, parsed.C1);
        Assert.Equal(128 / 255.0, parsed.Alpha);
    }

    [Fact]
    public void TryParse_TurnAngle_ConvertsToDegrees()
    {
        var parsed = ColorParser.TryParse("hsl(0.5turn 100% 50%)");

        Assert.NotNull(parsed);
        Assert.Equal(180, parsed.C1);
    }

    [Fact]
    public void TryParse_OutOfRange_Clamps()
    {
        var parsed = ColorParser.TryParse("rgb(300 -5 0)");

        Assert.NotNull(parsed);
        Assert.Equal(255, parsed.C1);
        Assert.Equal(0, parsed.C2);
        Assert.Equal(0, parsed.C3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notacolor")]
    [InlineData("transparent")]
    [InlineData("fff")]
    [InlineData("#12345")]
    [InlineData("#ggg")]
    [InlineData("rgb(255 0)")]
    [InlineData("hello world")]
    public void TryParse_InvalidColors_ReturnsNull(string? text)
    {
        Assert.Null(ColorParser.TryParse(text));
    }

    [Fact]
    public void TryParse_UppercaseFunction_Null()
    {
        // Parity with Copyous: functional notations are matched case-sensitively
        // (the original RegExp has no `i` flag); only named colors ignore case.
        Assert.Null(ColorParser.TryParse("RGB(255 0 0)"));
    }

    [Fact]
    public void TryParse_HugeInput_ReturnsNull()
    {
        Assert.Null(ColorParser.TryParse(new string('r', 600)));
    }

    [Fact]
    public void IsColor_MatchesTryParse()
    {
        Assert.True(ColorParser.IsColor("hsl(0 0% 0%)"));
        Assert.False(ColorParser.IsColor("nope"));
    }
}
