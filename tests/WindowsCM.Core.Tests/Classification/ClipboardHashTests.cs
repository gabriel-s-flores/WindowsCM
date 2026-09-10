// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class ClipboardHashTests
{
    [Fact]
    public void Md5Hex_MatchesRfcVector()
    {
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", ClipboardHash.Md5Hex("abc"));
    }

    [Fact]
    public void Md5Hex_EmptyString_MatchesRfcVector()
    {
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", ClipboardHash.Md5Hex(""));
    }

    [Fact]
    public void Md5Hex_Bytes_MatchUtf8String()
    {
        Assert.Equal(ClipboardHash.Md5Hex("abc"), ClipboardHash.Md5Hex(new byte[] { 97, 98, 99 }));
    }

    [Fact]
    public void FileHash_UriAndLocalForms_Agree()
    {
        var fromUri = ClipboardHash.FileHash(new[] { "file:///C:/a%20b.txt", "file:///C:/c.txt" });
        var fromLocal = ClipboardHash.FileHash(new[] { @"C:\a b.txt", @"C:\c.txt" });

        Assert.Equal(fromLocal, fromUri);
        Assert.Equal(ClipboardHash.Md5Hex(@"C:\a b.txt" + "\n" + @"C:\c.txt"), fromUri);
    }

    [Fact]
    public void ToLocalPath_FileUri_ResolvesDrivePath()
    {
        Assert.Equal(@"C:\a b.txt", ClipboardHash.ToLocalPath("file:///C:/a%20b.txt"));
    }

    [Fact]
    public void ToLocalPath_BarePath_StaysVerbatim()
    {
        Assert.Equal(@"C:\a.txt", ClipboardHash.ToLocalPath(@"C:\a.txt"));
    }

    [Fact]
    public void FileHash_BarePaths_HashVerbatim()
    {
        Assert.Equal(ClipboardHash.Md5Hex(@"C:\a.txt"), ClipboardHash.FileHash(new[] { @"C:\a.txt" }));
    }
}
