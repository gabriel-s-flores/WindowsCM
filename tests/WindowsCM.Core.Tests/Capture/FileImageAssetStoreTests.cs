// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Tests.Capture;

public sealed class FileImageAssetStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "wcm-img-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void SaveIfAbsent_WritesBytesAndReturnsFileUri()
    {
        var store = new FileImageAssetStore(_dir);
        var bytes = new byte[] { 1, 2, 3 };

        var uri = store.SaveIfAbsent("abc.png", bytes);

        Assert.Equal(new Uri(Path.Combine(_dir, "abc.png")).AbsoluteUri, uri);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_dir, "abc.png")));
    }

    [Fact]
    public void SaveIfAbsent_ExistingFile_KeepsFirstBytes()
    {
        var store = new FileImageAssetStore(_dir);
        store.SaveIfAbsent("abc.png", [1]);

        var uri = store.SaveIfAbsent("abc.png", [2]);

        Assert.Equal([1], File.ReadAllBytes(Path.Combine(_dir, "abc.png")));
        Assert.Equal(new Uri(Path.Combine(_dir, "abc.png")).AbsoluteUri, uri);
    }

    [Fact]
    public void SweepOrphans_KeepsReferencedDeletesRest()
    {
        var store = new FileImageAssetStore(_dir);
        store.SaveIfAbsent("keep.png", [1]);
        store.SaveIfAbsent("drop.png", [2]);

        store.SweepOrphans(["keep.png"]);

        Assert.True(File.Exists(Path.Combine(_dir, "keep.png")));
        Assert.False(File.Exists(Path.Combine(_dir, "drop.png")));
    }

    [Fact]
    public void SaveIfAbsent_LeavesNoTempFilesBehind()
    {
        var store = new FileImageAssetStore(_dir);

        store.SaveIfAbsent("a.png", [1]);
        store.SaveIfAbsent("a.png", [2]);

        Assert.Equal(["a.png"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    // Incognito folders of a session that never ended (crash, power loss)
    // kept screenshots in %TEMP% until uninstall; a live session of another
    // logon (recently written) is left alone.
    [Fact]
    public void SweepStaleSessions_RemovesOnlyOldIncognitoFolders()
    {
        Directory.CreateDirectory(_dir);
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var stale = Path.Combine(_dir, EphemeralImageAssetStore.DirectoryPrefix + "old");
        var live = Path.Combine(_dir, EphemeralImageAssetStore.DirectoryPrefix + "live");
        var unrelated = Path.Combine(_dir, "SomethingElse");
        foreach (var dir in new[] { stale, live, unrelated })
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "x.png"), [1]);
        }
        Directory.SetLastWriteTimeUtc(stale, now.AddDays(-3));
        Directory.SetLastWriteTimeUtc(live, now.AddMinutes(-5));
        Directory.SetLastWriteTimeUtc(unrelated, now.AddDays(-30));

        EphemeralImageAssetStore.SweepStaleSessions(TimeSpan.FromDays(1), _dir, now);

        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(live));
        Assert.True(Directory.Exists(unrelated));
    }
}
