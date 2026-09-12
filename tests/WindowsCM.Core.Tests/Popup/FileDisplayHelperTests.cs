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
