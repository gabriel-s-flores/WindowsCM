// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Text;
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Tests.Capture;

public sealed class ClipboardBlocksTests
{
    private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text);

    private static byte[] DropFiles(bool wide, uint? offset, params string[] paths)
    {
        var list = wide
            ? Encoding.Unicode.GetBytes(string.Concat(paths.Select(p => p + "\0")) + "\0")
            : Encoding.ASCII.GetBytes(string.Concat(paths.Select(p => p + "\0")) + "\0");
        var block = new byte[20 + list.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(block, offset ?? 20);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(16), wide ? 1 : 0);
        list.CopyTo(block, 20);
        return block;
    }

    [Fact]
    public void Text_StopsAtTheTerminator()
    {
        var block = Utf16("héllo 😀\0garbage after the terminator");

        Assert.Equal("héllo 😀", ClipboardBlocks.DecodeUnicodeText(block));
    }

    // A source app that sizes the block to the text and forgets the NUL:
    // the whole block, and not one byte past it.
    [Fact]
    public void Text_WithoutTerminator_IsTheWholeBlock()
    {
        Assert.Equal("no terminator", ClipboardBlocks.DecodeUnicodeText(Utf16("no terminator")));
    }

    [Fact]
    public void Text_OddOrEmptyBlocks_DoNotThrow()
    {
        var odd = Utf16("ab").Append((byte)0x41).ToArray();

        Assert.Equal("ab", ClipboardBlocks.DecodeUnicodeText(odd));
        Assert.Equal("", ClipboardBlocks.DecodeUnicodeText([]));
        Assert.Equal("", ClipboardBlocks.DecodeUnicodeText([0x41]));
    }

    [Fact]
    public void DropFiles_Wide_ListsEveryPath()
    {
        var block = DropFiles(wide: true, offset: null, @"C:\a.txt", @"D:\Fotos\ção 1.png", @"\\server\share\x.pdf");

        Assert.Equal(
            [@"C:\a.txt", @"D:\Fotos\ção 1.png", @"\\server\share\x.pdf"],
            ClipboardBlocks.ParseDropFiles(block));
    }

    [Fact]
    public void DropFiles_Ansi_IsLeftToTheSystem()
    {
        Assert.Null(ClipboardBlocks.ParseDropFiles(DropFiles(wide: false, offset: null, @"C:\a.txt")));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(19u)]
    [InlineData(4_000_000u)]
    [InlineData(uint.MaxValue)]
    public void DropFiles_OffsetOutsideTheBlock_ListsNothing(uint offset)
    {
        Assert.Empty(ClipboardBlocks.ParseDropFiles(DropFiles(wide: true, offset, @"C:\a.txt"))!);
    }

    [Fact]
    public void DropFiles_MissingFinalTerminators_StopsAtTheBlockEnd()
    {
        var block = DropFiles(wide: true, offset: null, @"C:\a.txt", @"C:\b.txt");
        var cut = block[..^6]; // drops "\0\0" and the last char of b.txt

        Assert.Equal([@"C:\a.txt", @"C:\b.tx"], ClipboardBlocks.ParseDropFiles(cut));
        Assert.Empty(ClipboardBlocks.ParseDropFiles(block[..10])!);
    }

    [Fact]
    public void DropFiles_FiftyThousandPaths_ParseInLinearTime()
    {
        var paths = Enumerable.Range(0, 50_000).Select(i => $@"C:\Users\someone\Pictures\Camera Roll\IMG_{i:D6}.jpg").ToArray();
        var block = DropFiles(wide: true, offset: null, paths);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var parsed = ClipboardBlocks.ParseDropFiles(block);

        Assert.Equal(50_000, parsed!.Count);
        Assert.Equal(paths[^1], parsed[^1]);
        Assert.True(watch.ElapsedMilliseconds < 2_000, $"{watch.ElapsedMilliseconds} ms");
    }
}
