// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using WindowsCM.Core.Capture.Win32;
using WindowsCM.Core.History;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

// The planner is pure: a stored item in, clipboard contents out. The image
// byte loader is a func so tests pass canned PNGs without touching disk.
public sealed class CopyBackPlannerTests
{
    private static readonly DateTime T0 = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    private static ClipboardItem Item(ItemKind kind, string content, string? metadata = null) =>
        new(kind, content, false, null, T0, metadata, null);

    private static byte[] Png1x1(byte r, byte g, byte b)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(1); w.Write(1);
        w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(4);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        w.Write(b); w.Write(g); w.Write(r); w.Write((byte)255);
        return DibToPng.FromDib(ms.ToArray());
    }

    [Theory]
    [InlineData(ItemKind.Text)]
    [InlineData(ItemKind.Code)]
    [InlineData(ItemKind.Link)]
    [InlineData(ItemKind.Character)]
    [InlineData(ItemKind.Color)]
    public void TextKinds_WriteVerbatimText(ItemKind kind)
    {
        var planned = CopyBackPlanner.Plan(Item(kind, "hello world"), _ => null);

        Assert.NotNull(planned);
        Assert.Equal("hello world", planned.Text);
        Assert.Null(planned.Html);
        Assert.Null(planned.FileLocalPaths);
        Assert.Null(planned.ImagePng);
    }

    [Fact]
    public void TextWithHtmlMetadata_RewrittenVerbatim()
    {
        const string envelope = "Version:1.0\r\nStartHTML:00000097\r\n<!--StartFragment-->hi<!--EndFragment-->";
        var metadata = JsonSerializer.Serialize(new { html = envelope });

        var planned = CopyBackPlanner.Plan(Item(ItemKind.Text, "hi", metadata), _ => null);

        Assert.NotNull(planned);
        Assert.Equal("hi", planned.Text);
        Assert.Equal(envelope, planned.Html);
    }

    [Fact]
    public void CorruptMetadata_DegradesToTextOnly()
    {
        var planned = CopyBackPlanner.Plan(Item(ItemKind.Text, "hi", "not-json{"), _ => null);

        Assert.NotNull(planned);
        Assert.Equal("hi", planned.Text);
        Assert.Null(planned.Html);
    }

    [Fact]
    public void FileKind_ResolvesUriToLocalPath()
    {
        var uri = new Uri(@"C:\a.txt").AbsoluteUri;

        var planned = CopyBackPlanner.Plan(Item(ItemKind.File, uri), _ => null);

        Assert.NotNull(planned);
        Assert.Null(planned.Text);
        Assert.Equal([@"C:\a.txt"], planned.FileLocalPaths);
    }

    [Fact]
    public void FilesKind_ResolvesEveryLine()
    {
        var content = string.Join("\n",
            new Uri(@"C:\a.txt").AbsoluteUri,
            new Uri(@"C:\b.txt").AbsoluteUri);

        var planned = CopyBackPlanner.Plan(Item(ItemKind.Files, content), _ => null);

        Assert.NotNull(planned);
        Assert.Null(planned.Text);
        Assert.Equal([@"C:\a.txt", @"C:\b.txt"], planned.FileLocalPaths);
    }

    [Fact]
    public void FileKind_ForcedCopy_IgnoresCutMetadata()
    {
        // Copyous parity (research 01 §2): copy-back always forces the copy
        // operation; the stored cut marker never reaches the clipboard.
        var uri = new Uri(@"C:\a.txt").AbsoluteUri;
        var planned = CopyBackPlanner.Plan(
            Item(ItemKind.File, uri, """{"operation":"cut"}"""), _ => null);

        Assert.NotNull(planned);
        Assert.Equal([@"C:\a.txt"], planned.FileLocalPaths);
    }

    [Fact]
    public void ImageKind_LoadsPngAndConvertsToDib()
    {
        var png = Png1x1(r: 255, g: 0, b: 0);
        var uri = new Uri(@"C:\img.png").AbsoluteUri;
        byte[]? SeenKey(string key)
        {
            Assert.Equal(uri, key);
            return png;
        }

        var planned = CopyBackPlanner.Plan(Item(ItemKind.Image, uri), SeenKey);

        Assert.NotNull(planned);
        Assert.Equal(png, planned.ImagePng);
        Assert.NotNull(planned.ImageDib);
        Assert.Null(planned.Text);
        // The converted DIB carries the red pixel back (BGRA, bottom-up).
        Assert.Equal([0, 0, 255, 255], planned.ImageDib[^4..]);
    }

    [Fact]
    public void ImageKind_MissingFile_ReturnsNull()
    {
        var planned = CopyBackPlanner.Plan(
            Item(ItemKind.Image, new Uri(@"C:\gone.png").AbsoluteUri), _ => null);

        Assert.Null(planned);
    }
}
