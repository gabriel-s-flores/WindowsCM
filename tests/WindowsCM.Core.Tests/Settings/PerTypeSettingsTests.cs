// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Six per-type screens with mapped defaults and ranges.
public sealed class PerTypeSettingsTests
{
    [Fact]
    public void TextDefaults_HideInfo_CountCharacters()
    {
        var text = new TextItemSettings();

        Assert.False(text.ShowInfo);
        Assert.Equal(TextCountMode.Characters, text.CountMode);
    }

    [Fact]
    public void CodeDefaults_HighlightAndNumbersOn()
    {
        var code = new CodeItemSettings();

        Assert.True(code.SyntaxHighlighting);
        Assert.True(code.ShowLineNumbers);
        Assert.False(code.ShowInfo);
        Assert.Equal(TextCountMode.Characters, code.CountMode);
    }

    [Fact]
    public void ImageDefaults_HideInfo_Cover()
    {
        var image = new ImageItemSettings();

        Assert.False(image.ShowInfo);
        Assert.Equal(BackgroundSize.Cover, image.Background);
    }

    [Fact]
    public void FileDefaults_PreviewOrInfo_AllTypes_ShellThumbnails()
    {
        var file = new FileItemSettings();

        Assert.Equal(FilePreviewVisibility.PreviewOrInfo, file.Visibility);
        Assert.Equal(FilePreviewTypes.All, file.Types);
        Assert.Empty(file.ExclusionGlobs);
        Assert.Equal(BackgroundSize.Cover, file.Background);
        Assert.True(file.SyntaxHighlighting);
        Assert.True(file.ShowLineNumbers);
        // Thumbnails resolve via Windows Shell providers (spec), so the
        // visibility knob keeps its Copyous meaning without GdkPixbuf.
        Assert.True(file.Types.HasFlag(FilePreviewTypes.Thumbnail));
    }

    [Fact]
    public void LinkDefaults_PreviewOn_Vertical_Contain()
    {
        var link = new LinkItemSettings();

        Assert.True(link.ShowPreview);
        Assert.True(link.ShowImage);
        Assert.Equal(BackgroundSize.Contain, link.ImageBackground);
        Assert.Equal(DialogOrientation.Vertical, link.Orientation);
        Assert.Empty(link.ExclusionPatterns);
    }

    [Fact]
    public void LinkExclusions_PatternSkipsPreview()
    {
        var link = new LinkItemSettings { ExclusionPatterns = ["^https://intranet\\."] };

        Assert.True(link.IsExcluded("https://intranet.local/page"));
        Assert.False(link.IsExcluded("https://example.com/"));
    }

    [Fact]
    public void LinkToPreviewOptions_MapsShowFlagsAndPatterns()
    {
        var link = new LinkItemSettings
        {
            ShowPreview = false,
            ShowImage = false,
            ExclusionPatterns = ["example"],
        };
        var options = link.ToPreviewOptions();

        Assert.False(options.ShowPreview);
        Assert.False(options.ShowImage);
        Assert.Equal(["example"], options.ExclusionPatterns);
    }

    [Fact]
    public void CharacterDefaults_MaxOne_HideUnicode()
    {
        var character = new CharacterItemSettings();

        Assert.Equal(1, character.MaxCharacters);
        Assert.False(character.ShowUnicode);
    }

    [Fact]
    public void CharacterClamp_PinsToOneThroughFour()
    {
        var low = new CharacterItemSettings { MaxCharacters = 0 };
        var high = new CharacterItemSettings { MaxCharacters = 99 };
        low.Clamp();
        high.Clamp();

        Assert.Equal(1, low.MaxCharacters);
        Assert.Equal(4, high.MaxCharacters);
    }
}
