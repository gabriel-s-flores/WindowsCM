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
    public void FileHash_StripsSchemeAndUnescapesBeforeHashing()
    {
        var uris = new[] { "file:///tmp/a%20b.txt", "file:///tmp/c.txt" };

        Assert.Equal(
            ClipboardHash.Md5Hex("/tmp/a b.txt\n/tmp/c.txt"),
            ClipboardHash.FileHash(uris));
    }

    [Fact]
    public void FileHash_BarePaths_HashVerbatim()
    {
        Assert.Equal(ClipboardHash.Md5Hex(@"C:\a.txt"), ClipboardHash.FileHash(new[] { @"C:\a.txt" }));
    }
}
