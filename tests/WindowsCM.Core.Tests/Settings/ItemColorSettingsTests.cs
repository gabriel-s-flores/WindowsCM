// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

public sealed class ItemColorSettingsTests
{
    [Fact]
    public void Defaults_AreAllEmptyStrings()
    {
        var colors = new ItemColorSettings();

        Assert.Equal("", colors.Link);
        Assert.Equal("", colors.Code);
        Assert.Equal("", colors.File);
        Assert.Equal("", colors.Image);
        Assert.Equal("", colors.Character);
        Assert.Equal("", colors.Color);
        Assert.Equal("", colors.Text);
        Assert.False(colors.IsCustomized(ItemKind.Link));
    }

    [Fact]
    public void GetAndSetCustomColor_NormalizesValidHex()
    {
        var colors = new ItemColorSettings();
        colors.SetCustomColor(ItemKind.Link, "#0078d4");
        colors.SetCustomColor(ItemKind.File, "FF8800");

        Assert.Equal("#0078D4", colors.GetCustomColor(ItemKind.Link));
        Assert.Equal("#FF8800", colors.GetCustomColor(ItemKind.File));
        Assert.Equal("#FF8800", colors.GetCustomColor(ItemKind.Files));
        Assert.True(colors.IsCustomized(ItemKind.Link));
        Assert.True(colors.IsCustomized(ItemKind.File));
    }

    [Fact]
    public void GetCustomColor_InvalidHexReturnsNull()
    {
        var colors = new ItemColorSettings();
        colors.SetCustomColor(ItemKind.Code, "not-a-color");

        Assert.Null(colors.GetCustomColor(ItemKind.Code));
        Assert.False(colors.IsCustomized(ItemKind.Code));
    }

    [Fact]
    public void Reset_ClearsSpecificKind()
    {
        var colors = new ItemColorSettings();
        colors.SetCustomColor(ItemKind.Image, "#00FF00");
        Assert.True(colors.IsCustomized(ItemKind.Image));

        colors.Reset(ItemKind.Image);

        Assert.Equal("", colors.Image);
        Assert.Null(colors.GetCustomColor(ItemKind.Image));
        Assert.False(colors.IsCustomized(ItemKind.Image));
    }

    [Fact]
    public void ResetAll_ClearsAllCustomColors()
    {
        var colors = new ItemColorSettings();
        colors.SetCustomColor(ItemKind.Link, "#112233");
        colors.SetCustomColor(ItemKind.Code, "#445566");

        colors.ResetAll();

        Assert.Null(colors.GetCustomColor(ItemKind.Link));
        Assert.Null(colors.GetCustomColor(ItemKind.Code));
        Assert.False(colors.IsCustomized(ItemKind.Link));
        Assert.False(colors.IsCustomized(ItemKind.Code));
    }

    [Fact]
    public void Serialization_RoundTripsThroughAppSettings()
    {
        var settings = AppSettings.Default();
        settings.ItemColors.SetCustomColor(ItemKind.Link, "#123456");
        settings.ItemColors.SetCustomColor(ItemKind.Character, "#ABCDEF");

        var json = SettingsStore.Serialize(settings);
        var restored = SettingsStore.Deserialize(json);

        Assert.Equal("#123456", restored.ItemColors.GetCustomColor(ItemKind.Link));
        Assert.Equal("#ABCDEF", restored.ItemColors.GetCustomColor(ItemKind.Character));
    }
}
