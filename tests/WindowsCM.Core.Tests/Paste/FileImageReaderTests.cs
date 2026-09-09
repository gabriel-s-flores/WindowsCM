// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

public sealed class FileImageReaderTests : IDisposable
{
    private readonly string _dir;

    public FileImageReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wcm-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void LoadPng_SavedBytes_RoundTrip()
    {
        var store = new FileImageAssetStore(_dir);
        var bytes = new byte[] { 9, 8, 7 };
        var content = store.SaveIfAbsent("abc123.png", bytes);

        Assert.Equal(bytes, new FileImageReader(_dir).LoadPng(content));
    }

    [Fact]
    public void LoadPng_MissingFile_ReturnsNull()
    {
        var reader = new FileImageReader(_dir);

        Assert.Null(reader.LoadPng(new Uri(Path.Combine(_dir, "gone.png")).AbsoluteUri));
        Assert.Null(reader.LoadPng("not-a-uri-at-all"));
    }
}
