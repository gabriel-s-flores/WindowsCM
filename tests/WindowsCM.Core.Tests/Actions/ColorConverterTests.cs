// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Actions;

public sealed class ColorConverterTests
{
    private static ParsedColor Parse(string text) => ColorParser.TryParse(text)!;

    private static string Convert(string text, ColorSpace space) =>
        ColorConverter.Format(ColorConverter.Convert(Parse(text), space));

    [Fact]
    public void Red_ConvertsToScreenSpaces()
    {
        Assert.Equal("rgb(255 0 0)", Convert("red", ColorSpace.Rgb));
        Assert.Equal("#ff0000", Convert("red", ColorSpace.Hex));
        Assert.Equal("hsl(0 100% 50%)", Convert("red", ColorSpace.Hsl));
        Assert.Equal("hwb(0 0% 0%)", Convert("red", ColorSpace.Hwb));
        Assert.Equal("color(srgb-linear 1 0 0)", Convert("red", ColorSpace.LinearRgb));
    }

    [Fact]
    public void Lime_ConvertsToHsl()
    {
        Assert.Equal("hsl(120 100% 50%)", Convert("#00ff00", ColorSpace.Hsl));
        Assert.Equal("rgb(0 255 0)", Convert("#00ff00", ColorSpace.Rgb));
    }

    [Fact]
    public void Alpha_FormatsAsSlashAndHexSuffix()
    {
        Assert.Equal("rgb(255 0 0 / 0.5)", Convert("rgba(255, 0, 0, 0.5)", ColorSpace.Rgb));
        Assert.Equal("#ff000080", Convert("rgba(255, 0, 0, 0.5)", ColorSpace.Hex));
        Assert.Equal(
            "hsl(0 100% 50% / 0.5)", Convert("hsla(0, 100%, 50%, 0.5)", ColorSpace.Hsl));
    }

    [Fact]
    public void Opaque_OmitsAlpha()
    {
        Assert.Equal("rgb(255 0 0)", Convert("red", ColorSpace.Rgb));
        Assert.DoesNotContain("/", Convert("red", ColorSpace.Oklch).Replace("oklch", string.Empty));
    }

    [Fact]
    public void Format_XyzUsesFourDigits()
    {
        var xyz = ParsedColor.Create(ColorSpace.Xyz, 0.9505, 1, 1.0888, 1);

        Assert.Equal("color(xyz 0.9505 1 1.0888)", ColorConverter.Format(xyz));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#00ff00")]
    [InlineData("hsl(210 50% 40%)")]
    [InlineData("white")]
    [InlineData("rebeccapurple")]
    public void WideSpaces_RoundTripThroughOwnOutput(string text)
    {
        foreach (var space in new[]
            {
                ColorSpace.LinearRgb, ColorSpace.Xyz, ColorSpace.Lab,
                ColorSpace.Lch, ColorSpace.Oklab, ColorSpace.Oklch,
            })
        {
            var once = ColorConverter.Convert(Parse(text), space);
            var formatted = ColorConverter.Format(once);
            // The formatted output is valid input for the ported grammar…
            var reparsed = ColorParser.TryParse(formatted);
            Assert.NotNull(reparsed);
            // …and converting it again is stable: the display string is
            // idempotent and components barely move (2dp display rounding).
            var twice = ColorConverter.Convert(reparsed!, space);
            Assert.Equal(formatted, ColorConverter.Format(twice));
            Assert.Equal(once.C1, twice.C1, precision: 0);
            Assert.Equal(once.C2, twice.C2, precision: 0);
            Assert.Equal(once.C3, twice.C3, precision: 0);
        }
    }

    [Fact]
    public void SameSpace_ReturnsInstanceUnchanged()
    {
        var parsed = Parse("red");

        Assert.Equal(parsed, ColorConverter.Convert(parsed, ColorSpace.Rgb));
    }
}
