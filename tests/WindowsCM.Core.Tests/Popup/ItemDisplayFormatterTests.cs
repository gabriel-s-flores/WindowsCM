// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class ItemDisplayFormatterTests
{
    [Fact]
    public void GetTitle_ImageItem_ReturnsGenericTitle_NeverExposesInternalUri()
    {
        var item = new ClipboardItem(
            ItemKind.Image,
            "file:///C:/Users/user/AppData/Local/WindowsCM/images/abcdef123456.png",
            false,
            null,
            DateTime.UtcNow,
            null,
            null);

        var title = ItemDisplayFormatter.GetTitle(item);

        Assert.Equal("Imagem", title);
        Assert.DoesNotContain("file://", title);
        Assert.DoesNotContain("AppData", title);
    }

    [Fact]
    public void GetTitle_FileItem_ReturnsFileName_NeverExposesAbsolutePath()
    {
        var item = new ClipboardItem(
            ItemKind.File,
            @"C:\Users\gabri\Pictures\vacation_photo.png",
            false,
            null,
            DateTime.UtcNow,
            null,
            null);

        var title = ItemDisplayFormatter.GetTitle(item);

        Assert.Equal("vacation_photo.png", title);
        Assert.DoesNotContain(@"C:\", title);
    }

    [Fact]
    public void GetTitle_FileItemWithFileUri_ReturnsFileName()
    {
        var item = new ClipboardItem(
            ItemKind.File,
            "file:///C:/Users/gabri/Documents/report.pdf",
            false,
            null,
            DateTime.UtcNow,
            null,
            null);

        var title = ItemDisplayFormatter.GetTitle(item);

        Assert.Equal("report.pdf", title);
    }

    [Fact]
    public void GetTitle_FilesItem_ReturnsCountAndFirstFileName()
    {
        var item = new ClipboardItem(
            ItemKind.Files,
            @"C:\Photos\img1.jpg" + "\n" + @"C:\Photos\img2.jpg" + "\n" + @"C:\Photos\img3.jpg",
            false,
            null,
            DateTime.UtcNow,
            null,
            null);

        var title = ItemDisplayFormatter.GetTitle(item);

        Assert.Equal("3 arquivos (img1.jpg)", title);
    }

    [Fact]
    public void GetTitle_CustomTitle_ReturnsCustomTitle()
    {
        var item = new ClipboardItem(
            ItemKind.File,
            @"C:\Temp\test.txt",
            false,
            null,
            DateTime.UtcNow,
            null,
            "Meu Arquivo Customizado");

        var title = ItemDisplayFormatter.GetTitle(item);

        Assert.Equal("Meu Arquivo Customizado", title);
    }

    [Fact]
    public void GetTypeLabel_IdentifiesImageFormats()
    {
        var png = new ClipboardItem(ItemKind.File, @"C:\path\image.png", false, null, DateTime.UtcNow, null, null);
        var jpg = new ClipboardItem(ItemKind.File, @"C:\path\photo.jpeg", false, null, DateTime.UtcNow, null, null);
        var gif = new ClipboardItem(ItemKind.File, @"C:\path\anim.gif", false, null, DateTime.UtcNow, null, null);
        var directImg = new ClipboardItem(ItemKind.Image, "file:///path/hash.png", false, null, DateTime.UtcNow, null, null);

        Assert.Equal("Imagem PNG", ItemDisplayFormatter.GetTypeLabel(png));
        Assert.Equal("Imagem JPEG", ItemDisplayFormatter.GetTypeLabel(jpg));
        Assert.Equal("Imagem GIF", ItemDisplayFormatter.GetTypeLabel(gif));
        Assert.Equal("Imagem PNG", ItemDisplayFormatter.GetTypeLabel(directImg));
    }

    [Fact]
    public void GetTypeLabel_IdentifiesCodeAndDocumentFormats()
    {
        var cs = new ClipboardItem(ItemKind.File, @"C:\repo\Service.cs", false, null, DateTime.UtcNow, null, null);
        var pdf = new ClipboardItem(ItemKind.File, @"C:\docs\manual.pdf", false, null, DateTime.UtcNow, null, null);
        var zip = new ClipboardItem(ItemKind.File, @"C:\downloads\archive.zip", false, null, DateTime.UtcNow, null, null);
        var mp4 = new ClipboardItem(ItemKind.File, @"C:\videos\clip.mp4", false, null, DateTime.UtcNow, null, null);

        Assert.Equal("Código C#", ItemDisplayFormatter.GetTypeLabel(cs));
        Assert.Equal("Documento PDF", ItemDisplayFormatter.GetTypeLabel(pdf));
        Assert.Equal("Arquivo Compactado", ItemDisplayFormatter.GetTypeLabel(zip));
        Assert.Equal("Vídeo MP4", ItemDisplayFormatter.GetTypeLabel(mp4));
    }

    [Fact]
    public void GetTypeLabel_CodeWithLanguageMetadata_ShowsLanguage()
    {
        var item = new ClipboardItem(
            ItemKind.Code,
            "public void Hello() => Console.WriteLine();",
            false,
            null,
            DateTime.UtcNow,
            "{\"language\":{\"id\":\"csharp\",\"name\":\"C#\"}}",
            null);

        var label = ItemDisplayFormatter.GetTypeLabel(item);

        Assert.Equal("Código (C#)", label);
    }

    [Fact]
    public void GetPreviewText_ReturnsMultipleLinesPreservingIndent()
    {
        var code = "public class Foo\n{\n    public void Bar()\n    {\n        return;\n    }\n}";
        var item = new ClipboardItem(ItemKind.Code, code, false, null, DateTime.UtcNow, null, null);

        var preview = ItemDisplayFormatter.GetPreviewText(item, 8);

        Assert.Contains("public class Foo", preview);
        Assert.Contains("    public void Bar()", preview);
        Assert.Contains("        return;", preview);
    }

    [Fact]
    public void GetKindIconGlyph_ReturnsExpectedGlyphs()
    {
        Assert.Equal("\uE943", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Code));
        Assert.Equal("\uE8A5", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Text));
        Assert.Equal("\uEB9F", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Image));
        Assert.Equal("\uED43", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.File));
        Assert.Equal("\uED25", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Files));
        Assert.Equal("\uE71B", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Link));
        Assert.Equal("\uE790", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Color));
        Assert.Equal("\uE76E", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Character, "A"));
        Assert.Equal("\uED53", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Character, "🚀"));
    }

    [Fact]
    public void GetTitle_EmojiCharacter_ReturnsEmoji()
    {
        var item = new ClipboardItem(ItemKind.Character, "🚀", false, null, DateTime.UtcNow, null, null);
        Assert.Equal("Emoji", ItemDisplayFormatter.GetTitle(item));
    }

    [Fact]
    public void GetTitle_MultipleEmojis_ReturnsEmoji()
    {
        var item = new ClipboardItem(ItemKind.Character, "🚀🎉❤️", false, null, DateTime.UtcNow, null, null);
        Assert.Equal("Emoji", ItemDisplayFormatter.GetTitle(item));
    }

    [Fact]
    public void GetTypeLabel_EmojiCharacter_ReturnsEmojiWithCodePoint()
    {
        var item = new ClipboardItem(ItemKind.Character, "🚀", false, null, DateTime.UtcNow, null, null);
        var label = ItemDisplayFormatter.GetTypeLabel(item);
        Assert.Equal("Emoji • U+1F680", label);
    }

    [Fact]
    public void GetTypeLabel_MultipleEmojis_ReturnsEmojiWithCount()
    {
        var item = new ClipboardItem(ItemKind.Character, "🚀🎉❤️", false, null, DateTime.UtcNow, null, null);
        var label = ItemDisplayFormatter.GetTypeLabel(item);
        Assert.Equal("Emoji • 3 emojis", label);
    }

    [Fact]
    public void GetKindIconGlyph_MultipleEmojis_ReturnsEmojiSmileyGlyph()
    {
        Assert.Equal("\uED53", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.Character, "🚀🎉"));
    }

    [Fact]
    public void GetTypeLabel_StandardCharacter_ReturnsCaractereWithCodePoint()
    {
        var item = new ClipboardItem(ItemKind.Character, "A", false, null, DateTime.UtcNow, null, null);
        var label = ItemDisplayFormatter.GetTypeLabel(item);
        Assert.Equal("Caractere • U+0041", label);
    }

    [Fact]
    public void GetTypeLabel_Text_ReturnsCaracteresInPortuguese()
    {
        var item = new ClipboardItem(ItemKind.Text, "Olá mundo", false, null, DateTime.UtcNow, null, null);
        var label = ItemDisplayFormatter.GetTypeLabel(item);
        Assert.Equal("Texto • 9 caracteres", label);
    }

    [Fact]
    public void IsEmoji_DetectsCommonEmojisAndRejectsRegularLetters()
    {
        Assert.True(ItemDisplayFormatter.IsEmoji("🚀"));
        Assert.True(ItemDisplayFormatter.IsEmoji("🎉"));
        Assert.True(ItemDisplayFormatter.IsEmoji("❤️"));
        Assert.False(ItemDisplayFormatter.IsEmoji("A"));
        Assert.False(ItemDisplayFormatter.IsEmoji("123"));
        Assert.False(ItemDisplayFormatter.IsEmoji(""));
    }

    [Fact]
    public void GetUnicodeCodePoint_ReturnsFormattedCodePoint()
    {
        Assert.Equal("U+1F680", ItemDisplayFormatter.GetUnicodeCodePoint("🚀"));
        Assert.Equal("U+0041", ItemDisplayFormatter.GetUnicodeCodePoint("A"));
    }

    [Fact]
    public void GetTypeLabel_WithCustomCategory_UsesCustomCategoryName()
    {
        var cats = new WindowsCM.Core.Settings.FileCategorySettings();
        cats.AddCategory("Modelos 3D", "#9B59B6", new[] { ".blend", ".obj" });

        var item = new ClipboardItem(ItemKind.File, @"C:\Projects\robot.blend", false, null, DateTime.UtcNow, null, null);
        var label = ItemDisplayFormatter.GetTypeLabel(item, cats);

        Assert.Equal("Modelos 3D • BLEND", label);
    }

    [Fact]
    public void GetKindIconGlyph_WithFileCategories_ReturnsSpecificIconForFileTypes()
    {
        var cats = new WindowsCM.Core.Settings.FileCategorySettings();

        Assert.Equal("\uE714", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.File, "video.mp4", cats)); // Video
        Assert.Equal("\uEC4F", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.File, "audio.mp3", cats)); // Audio
        Assert.Equal("\uE8AD", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.File, "slides.pptx", cats)); // Presentation
        Assert.Equal("\uF0E3", ItemDisplayFormatter.GetKindIconGlyph(ItemKind.File, "data.xlsx", cats)); // Spreadsheet
    }
}
