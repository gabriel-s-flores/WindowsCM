// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Disk seam in temp dirs (FileImageAssetStore pattern): thumbnails cached
// by URL hash (Copyous getCachePath + MD5(url) parity).
public sealed class LinkImageCacheTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "wcm-link-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void FileNameFor_UsesMd5OfUrl()
    {
        const string url = "https://example.com/articles/1";

        Assert.Equal(ClipboardHash.Md5Hex(url), LinkImageCache.FileNameFor(url));
    }

    [Fact]
    public void SaveIfAbsent_WritesBytesAndTryGetFindsThem()
    {
        var cache = new LinkImageCache(_dir);
        const string url = "https://example.com/a.png";

        var path = cache.SaveIfAbsent(url, [1, 2, 3]);

        Assert.Equal(Path.Combine(_dir, ClipboardHash.Md5Hex(url)), path);
        Assert.Equal(path, cache.TryGet(url));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
    }

    [Fact]
    public void SaveIfAbsent_ExistingFile_KeepsFirstBytes()
    {
        var cache = new LinkImageCache(_dir);
        const string url = "https://example.com/a.png";
        cache.SaveIfAbsent(url, [1]);

        cache.SaveIfAbsent(url, [2]);

        Assert.Equal([1], File.ReadAllBytes(Path.Combine(_dir, ClipboardHash.Md5Hex(url))));
    }

    [Fact]
    public void TryGet_Missing_ReturnsNull()
    {
        var cache = new LinkImageCache(_dir);

        Assert.Null(cache.TryGet("https://example.com/missing"));
    }

    [Fact]
    public void SweepOrphans_KeepsReferencedDeletesRest()
    {
        var cache = new LinkImageCache(_dir);
        const string keep = "https://example.com/keep";
        const string drop = "https://example.com/drop";
        cache.SaveIfAbsent(keep, [1]);
        cache.SaveIfAbsent(drop, [2]);

        cache.SweepOrphans([keep]);

        Assert.NotNull(cache.TryGet(keep));
        Assert.Null(cache.TryGet(drop));
    }
}
