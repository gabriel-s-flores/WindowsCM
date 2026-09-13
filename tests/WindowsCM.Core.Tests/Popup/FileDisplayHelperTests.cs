// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class FileDisplayHelperTests
{
    private static readonly DateTime T0 = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, "")]
    [InlineData(-1L, "")]
    [InlineData(0L, "0 B")]
    [InlineData(500L, "500 B")]
    [InlineData(1024L, "1,0 KB")]
    [InlineData(1536L, "1,5 KB")]
    [InlineData(1048576L, "1,0 MB")]
    [InlineData(2500000L, "2,4 MB")]
    [InlineData(1073741824L, "1,0 GB")]
    public void FormatFileSize_FormatsExpectedUnits(long? bytes, string expected)
    {
        var result = FileDisplayHelper.FormatFileSize(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(0, "")]
    [InlineData(5, "00:05")]
    [InlineData(59, "00:59")]
    [InlineData(65, "01:05")]
    [InlineData(235, "03:55")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725, "1:02:05")]
    public void FormatDuration_FormatsExpectedUnits(int? seconds, string expected)
    {
        TimeSpan? ts = seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null;
        var result = FileDisplayHelper.FormatDuration(ts);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AudioMetadataInfo_Summaries_FormatProperly()
    {
        var full = new AudioMetadataInfo(
            Title: "Shooting Stars",
            Artist: "Bag Raiders",
            Album: "Bag Raiders",
            Duration: TimeSpan.FromSeconds(235),
            FormattedDuration: "03:55",
            FileSizeBytes: 34200000L,
            FormattedSize: "32,6 MB");

        Assert.True(full.HasArtistOrAlbum);
        Assert.Equal("Bag Raiders • Bag Raiders", full.ArtistAndAlbumSummary);
        Assert.Equal("03:55 • 32,6 MB", full.DurationAndSizeSummary);

        var artistOnly = new AudioMetadataInfo(
            Title: "Track 1",
            Artist: "Solo Artist",
            Album: "",
            Duration: null,
            FormattedDuration: "",
            FileSizeBytes: 1048576L,
            FormattedSize: "1,0 MB");

        Assert.True(artistOnly.HasArtistOrAlbum);
        Assert.Equal("Solo Artist", artistOnly.ArtistAndAlbumSummary);
        Assert.Equal("1,0 MB", artistOnly.DurationAndSizeSummary);
    }

    [Fact]
    public void GetFileDetails_SingleFile_ExtractsCleanNames()
    {
        var item = new ClipboardItem(
            ItemKind.File,
            @"C:\Users\gabri\Documents\Relatorio.pdf",
            false, null, T0, null, null);

        var details = FileDisplayHelper.GetFileDetails(item, _ => 2500000L);

        Assert.NotNull(details);
        Assert.Equal("Relatorio.pdf", details.FileName);
        Assert.Equal(".pdf", details.Extension);
        Assert.Equal("Documento PDF", details.TypeLabel);
        Assert.Equal("2,4 MB", details.FormattedSize);
        Assert.Equal(@"C:\Users\gabri\Documents", details.DirectoryPath);
        Assert.False(details.IsMultiple);
    }

    [Fact]
    public void GetFileDetails_FileWithFileUri_ResolvesProperly()
    {
        var item = new ClipboardItem(
            ItemKind.File,
            "file:///C:/Downloads/setup.exe",
            false, null, T0, null, null);

        var details = FileDisplayHelper.GetFileDetails(item, _ => null);

        Assert.NotNull(details);
        Assert.Equal("setup.exe", details.FileName);
        Assert.Equal(".exe", details.Extension);
        Assert.Equal("Aplicativo Executável", details.TypeLabel);
        Assert.Equal("", details.FormattedSize);
        Assert.False(details.IsMultiple);
    }

    [Fact]
    public void GetFileDetails_MultipleFiles_SummarizesList()
    {
        var content = string.Join("\n",
            @"C:\Docs\Planilha.xlsx",
            @"C:\Docs\Texto.txt",
            @"C:\Docs\Foto.png");

        var item = new ClipboardItem(
            ItemKind.Files,
            content,
            false, null, T0, null, null);

        var details = FileDisplayHelper.GetFileDetails(item, _ => 1024L);

        Assert.NotNull(details);
        Assert.True(details.IsMultiple);
        Assert.Equal(3, details.FileCount);
        Assert.Equal("3 arquivos", details.FileName);
        Assert.Equal(3, details.Items.Count);
        Assert.Equal("Planilha.xlsx", details.Items[0].FileName);
        Assert.Equal("Texto.txt", details.Items[1].FileName);
        Assert.Equal("Foto.png", details.Items[2].FileName);
    }
}
