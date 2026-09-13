// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class ItemTypeThemeTests
{
    [Theory]
    [InlineData(ItemKind.Link, true, "#0067B8")]
    [InlineData(ItemKind.Link, false, "#4CC2FF")]
    [InlineData(ItemKind.Code, true, "#6366F1")]
    [InlineData(ItemKind.Code, false, "#A78BFA")]
    [InlineData(ItemKind.File, true, "#D97706")]
    [InlineData(ItemKind.File, false, "#FBBF24")]
    [InlineData(ItemKind.Files, true, "#D97706")]
    [InlineData(ItemKind.Files, false, "#FBBF24")]
    [InlineData(ItemKind.Image, true, "#16A34A")]
    [InlineData(ItemKind.Image, false, "#4ADE80")]
    [InlineData(ItemKind.Character, true, "#E11D48")]
    [InlineData(ItemKind.Character, false, "#FB7185")]
    [InlineData(ItemKind.Text, true, "#475569")]
    [InlineData(ItemKind.Text, false, "#94A3B8")]
    public void GetKindAccentHex_ReturnsDistinctColorsPerTheme(ItemKind kind, bool isLight, string expectedHex)
    {
        var hex = ItemTypeTheme.GetKindAccentHex(kind, isLight);
        Assert.Equal(expectedHex, hex);
    }

    [Fact]
    public void GetKindAccentHex_ColorItem_UsesParsedHexIfValid()
    {
        var hex = ItemTypeTheme.GetKindAccentHex(ItemKind.Color, true, "#FF4400");
        Assert.Equal("#FF4400", hex);
    }

    [Fact]
    public void GetKindBackgroundHex_HasAlphaChannelForSubtleTint()
    {
        var bgLight = ItemTypeTheme.GetKindBackgroundHex(ItemKind.Link, isLight: true);
        var bgDark = ItemTypeTheme.GetKindBackgroundHex(ItemKind.Link, isLight: false);

        // Expect #AARRGGBB format (9 characters including #)
        Assert.Equal(9, bgLight.Length);
        Assert.Equal(9, bgDark.Length);
        Assert.StartsWith("#1F", bgLight);
        Assert.StartsWith("#26", bgDark);
    }

    [Fact]
    public void GetKindAccentHex_WithCustomColors_OverridesDefault()
    {
        var custom = new WindowsCM.Core.Settings.ItemColorSettings();
        custom.SetCustomColor(ItemKind.Link, "#FF1493");
        custom.SetCustomColor(ItemKind.Code, "#00FA9A");

        var linkHex = ItemTypeTheme.GetKindAccentHex(ItemKind.Link, isLight: true, customColors: custom);
        var codeHex = ItemTypeTheme.GetKindAccentHex(ItemKind.Code, isLight: false, customColors: custom);
        var fileHex = ItemTypeTheme.GetKindAccentHex(ItemKind.File, isLight: true, customColors: custom);

        Assert.Equal("#FF1493", linkHex);
        Assert.Equal("#00FA9A", codeHex);
        Assert.Equal("#D97706", fileHex); // File was not customized, returns default
    }

    [Fact]
    public void GetKindAccentHex_WithInvalidCustomColor_FallsBackToDefault()
    {
        var custom = new WindowsCM.Core.Settings.ItemColorSettings();
        custom.Link = "bad-color";

        var linkHex = ItemTypeTheme.GetKindAccentHex(ItemKind.Link, isLight: true, customColors: custom);
        Assert.Equal("#0067B8", linkHex);
    }

    [Fact]
    public void GetKindBackgroundHex_WithCustomColor_CalculatesAlphaFromCustom()
    {
        var custom = new WindowsCM.Core.Settings.ItemColorSettings();
        custom.SetCustomColor(ItemKind.Link, "#FF1493");

        var bgLight = ItemTypeTheme.GetKindBackgroundHex(ItemKind.Link, isLight: true, customColors: custom);
        var bgDark = ItemTypeTheme.GetKindBackgroundHex(ItemKind.Link, isLight: false, customColors: custom);

        Assert.Equal("#1FFF1493", bgLight);
        Assert.Equal("#26FF1493", bgDark);
    }

    [Fact]
    public void GetKindAccentHex_FileItem_UsesFileCategoryColor()
    {
        var cats = new WindowsCM.Core.Settings.FileCategorySettings();
        var videoHex = ItemTypeTheme.GetKindAccentHex(ItemKind.File, isLight: true, content: "movie.mp4", fileCategories: cats);
        var presentationHex = ItemTypeTheme.GetKindAccentHex(ItemKind.File, isLight: false, content: "deck.pptx", fileCategories: cats);

        Assert.Equal("#DC2626", videoHex);
        Assert.Equal("#EA580C", presentationHex);
    }
}
