// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Classification;

public sealed class ClassifierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassifyText_BlankInput_Discarded(string? text)
    {
        Assert.Null(Classifier.ClassifyText(text));
    }

    [Fact]
    public void ClassifyText_Link_Detected()
    {
        var classified = Classifier.ClassifyText("https://example.com");

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Link, classified.Kind);
        Assert.Equal("https://example.com", classified.Content);
        Assert.Null(classified.MetadataJson);
    }

    [Fact]
    public void ClassifyText_LinkWithPadding_ContentPreservedVerbatim()
    {
        // Parity: detection runs on the trimmed text, the stored content is raw.
        var classified = Classifier.ClassifyText("  https://example.com  ");

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Link, classified.Kind);
        Assert.Equal("  https://example.com  ", classified.Content);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("😀")]
    public void ClassifyText_SingleGrapheme_Character(string text)
    {
        var classified = Classifier.ClassifyText(text);

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Character, classified.Kind);
        Assert.Equal(text, classified.Content);
    }

    [Fact]
    public void ClassifyText_TwoGraphemesWithRaisedLimit_Character()
    {
        var classified = Classifier.ClassifyText("ab", maxCharacters: 2);

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Character, classified.Kind);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#ff0000")]
    [InlineData("rgb(255 0 0)")]
    public void ClassifyText_ColorStrings_Detected(string text)
    {
        var classified = Classifier.ClassifyText(text);

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Color, classified.Kind);
    }

    [Fact]
    public void ClassifyText_CodeSnippet_DetectedWithoutLanguageYet()
    {
        var classified = Classifier.ClassifyText("public void Foo() { return 1; }");

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Code, classified.Kind);
        // v1 carries no language id: the hljs→AvalonEdit map spike (spec
        // Further Notes) resolves the language when previews land.
        Assert.Null(classified.MetadataJson);
    }

    [Fact]
    public void ClassifyText_PlainSentence_FallsBackToText()
    {
        var classified = Classifier.ClassifyText("just a note to self");

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Text, classified.Kind);
        Assert.Equal("just a note to self", classified.Content);
    }

    [Fact]
    public void ClassifyText_HashMatchesMd5Vector()
    {
        var classified = Classifier.ClassifyText("abc");

        Assert.NotNull(classified);
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", classified.ContentHash);
    }

    [Fact]
    public void ClassifyFiles_SinglePath_FileWithCopyMetadata()
    {
        var classified = Classifier.ClassifyFiles(
            new FileSnapshot(["file:///C:/a.txt"], FileOperation.Copy));

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.File, classified.Kind);
        Assert.Equal(@"C:\a.txt", classified.Content);
        Assert.Equal("""{"operation":"copy"}""", classified.MetadataJson);
        Assert.Equal(ClipboardHash.FileHash(["file:///C:/a.txt"]), classified.ContentHash);
    }

    [Fact]
    public void ClassifyFiles_TwoPaths_FilesJoinedWithCutMetadata()
    {
        var classified = Classifier.ClassifyFiles(new FileSnapshot(
            ["file:///C:/a.txt", "file:///C:/b.txt"], FileOperation.Cut));

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Files, classified.Kind);
        Assert.Equal(@"C:\a.txt" + "\n" + @"C:\b.txt", classified.Content);
        Assert.Equal("""{"operation":"cut"}""", classified.MetadataJson);
    }

    [Fact]
    public void ClassifyFiles_UriAndLocalForms_CanonicalizeEqual()
    {
        var fromUri = Classifier.ClassifyFiles(
            new FileSnapshot(["file:///C:/a%20b.txt"], FileOperation.Copy));
        var fromLocal = Classifier.ClassifyFiles(
            new FileSnapshot([@"C:\a b.txt"], FileOperation.Copy));

        Assert.NotNull(fromUri);
        Assert.NotNull(fromLocal);
        Assert.Equal(fromLocal.Content, fromUri.Content);
        Assert.Equal(fromLocal.ContentHash, fromUri.ContentHash);
    }

    [Fact]
    public void ClassifyFiles_HashAgreesWithStoredContent()
    {
        var classified = Classifier.ClassifyFiles(new FileSnapshot(
            ["file:///C:/a%20b.txt", "file:///C:/c.txt"], FileOperation.Copy));

        Assert.NotNull(classified);
        Assert.Equal(ClipboardHash.Md5Hex(classified.Content), classified.ContentHash);
    }

    [Theory]
    [InlineData(null)]
    public void ClassifyFiles_Null_ReturnsNull(FileSnapshot? files)
    {
        Assert.Null(Classifier.ClassifyFiles(files));
    }

    [Fact]
    public void ClassifyFiles_EmptyPaths_ReturnsNull()
    {
        Assert.Null(Classifier.ClassifyFiles(new FileSnapshot([], FileOperation.Copy)));
    }

    [Fact]
    public void ClassifyImage_PngBytes_HashDotExtension()
    {
        var data = new byte[] { 1, 2, 3 };

        var classified = Classifier.ClassifyImage(new ImageSnapshot("image/png", data));

        Assert.NotNull(classified);
        Assert.Equal(ClipboardHash.Md5Hex(data), classified.ContentHash);
        Assert.Equal("png", classified.Extension);
        Assert.Equal($"{classified.ContentHash}.png", classified.FileName);
    }

    [Fact]
    public void ClassifyImage_JpegMime_MapsExtension()
    {
        var classified = Classifier.ClassifyImage(new ImageSnapshot("image/jpeg", [9]));

        Assert.NotNull(classified);
        Assert.Equal("jpeg", classified.Extension);
    }

    [Theory]
    [InlineData(null)]
    public void ClassifyImage_Null_ReturnsNull(ImageSnapshot? image)
    {
        Assert.Null(Classifier.ClassifyImage(image));
    }

    [Fact]
    public void ClassifyImage_EmptyBytes_ReturnsNull()
    {
        Assert.Null(Classifier.ClassifyImage(new ImageSnapshot("image/png", [])));
    }

    [Fact]
    public void ClassifyImage_BlankMime_ReturnsNull()
    {
        Assert.Null(Classifier.ClassifyImage(new ImageSnapshot("  ", [1])));
    }

    [Fact]
    public void Probe_ImageBeatsFileBeatsText()
    {
        var image = new ImageSnapshot("image/png", [1]);
        var files = new FileSnapshot(["file:///tmp/a.txt"], FileOperation.Copy);

        var classified = Classifier.Probe(image, files, "hello");

        Assert.IsType<ClassifiedImage>(classified);
    }

    [Fact]
    public void Probe_FilesBeatText()
    {
        var files = new FileSnapshot(["file:///tmp/a.txt"], FileOperation.Copy);

        var classified = Classifier.Probe(null, files, "hello");

        var file = Assert.IsType<ClassifiedFile>(classified);
        Assert.Equal(ItemKind.File, file.Kind);
    }

    [Fact]
    public void Probe_TextOnly_ClassifiesText()
    {
        var classified = Classifier.Probe(null, null, "hello");

        var text = Assert.IsType<ClassifiedText>(classified);
        Assert.Equal(ItemKind.Text, text.Kind);
    }

    [Fact]
    public void Probe_AllEmpty_ReturnsNull()
    {
        Assert.Null(Classifier.Probe(null, null, null));
        Assert.Null(Classifier.Probe(null, null, "   "));
        Assert.Null(Classifier.Probe(
            new ImageSnapshot("image/png", []),
            new FileSnapshot([], FileOperation.Copy),
            null));
    }

    [Fact]
    public void Probe_DiscardedImageFallsThroughToText()
    {
        var classified = Classifier.Probe(new ImageSnapshot("image/png", []), null, "hello");

        var text = Assert.IsType<ClassifiedText>(classified);
        Assert.Equal("hello", text.Content);
    }

    [Fact]
    public void Probe_SensitiveHint_RejectsEverything()
    {
        var image = new ImageSnapshot("image/png", [1]);

        Assert.Null(Classifier.Probe(image, null, "hello", formats: ["x-kde-passwordManagerHint"]));
        Assert.Null(Classifier.Probe(null, null, "hello",
            formats: ["ExcludeClipboardContentFromMonitorProcessing"]));
    }

    [Fact]
    public void Probe_PlainFormats_CaptureProceeds()
    {
        var classified = Classifier.Probe(null, null, "hello", formats: ["Text", "CF_HDROP"]);

        Assert.IsType<ClassifiedText>(classified);
    }

    [Theory]
    [InlineData("🚀🎉")]
    [InlineData("😀😁😂🤣")]
    [InlineData("❤️🔥✨")]
    [InlineData("🚀  🎉")]
    [InlineData("🇧🇷 🇺🇸")]
    public void ClassifyText_MultipleEmojisOnly_ClassifiedAsCharacter(string emojiText)
    {
        var classified = Classifier.ClassifyText(emojiText);

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Character, classified.Kind);
        Assert.Equal(emojiText, classified.Content);
    }

    [Theory]
    [InlineData("Hello 🚀")]
    [InlineData("🚀 123")]
    [InlineData("Texto com emoji 😀")]
    [InlineData("🚀.")]
    public void ClassifyText_MixedTextWithEmoji_ClassifiedAsText(string mixedText)
    {
        var classified = Classifier.ClassifyText(mixedText);

        Assert.NotNull(classified);
        Assert.Equal(ItemKind.Text, classified.Kind);
        Assert.Equal(mixedText, classified.Content);
    }
}

