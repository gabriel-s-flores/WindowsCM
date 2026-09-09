// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class FileDropTests
{
    [Fact]
    public void Parse_CopyPlusTwoPaths_CopyWithBoth()
    {
        var parsed = FileDrop.Parse(["copy", "file:///tmp/a.txt", "file:///tmp/b.txt"]);

        Assert.NotNull(parsed);
        Assert.Equal(FileOperation.Copy, parsed.Operation);
        Assert.Equal(["file:///tmp/a.txt", "file:///tmp/b.txt"], parsed.Paths);
    }

    [Fact]
    public void Parse_UppercaseCut_ParsesCut()
    {
        var parsed = FileDrop.Parse(["CUT", "file:///tmp/a.txt"]);

        Assert.NotNull(parsed);
        Assert.Equal(FileOperation.Cut, parsed.Operation);
        Assert.Equal(["file:///tmp/a.txt"], parsed.Paths);
    }

    [Fact]
    public void Parse_NoOperationLine_DefaultsToCopy()
    {
        var parsed = FileDrop.Parse(["file:///tmp/a.txt"]);

        Assert.NotNull(parsed);
        Assert.Equal(FileOperation.Copy, parsed.Operation);
        Assert.Equal(["file:///tmp/a.txt"], parsed.Paths);
    }

    [Fact]
    public void Parse_TrimsEntriesAndDropsBlanks()
    {
        var parsed = FileDrop.Parse(["copy", "  file:///tmp/a.txt  ", "   "]);

        Assert.NotNull(parsed);
        Assert.Equal(["file:///tmp/a.txt"], parsed.Paths);
    }

    [Theory]
    [InlineData(null)]
    public void Parse_Null_ReturnsNull(string[]? lines)
    {
        Assert.Null(FileDrop.Parse(lines));
    }

    [Theory]
    [InlineData()]
    [InlineData("copy")]
    [InlineData("cut")]
    [InlineData("   ")]
    public void Parse_NoPaths_ReturnsNull(params string[] lines)
    {
        Assert.Null(FileDrop.Parse(lines));
    }

    [Theory]
    [InlineData(1, FileOperation.Copy)]
    [InlineData(2, FileOperation.Cut)]
    [InlineData(0, FileOperation.Copy)]
    [InlineData(3, FileOperation.Cut)]
    public void FromDropEffect_MasksMoveBit(int effect, FileOperation expected)
    {
        // Win32 Preferred DropEffect: DROPEFFECT_COPY=1, DROPEFFECT_MOVE=2.
        // Bit 3 (copy|move) still means cut: mask, never compare equality.
        Assert.Equal(expected, FileDrop.FromDropEffect(effect));
    }
}
