// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

public sealed class CacheCleanerTests
{
    [Fact]
    public void Clear_EmptyDirectory_ReturnsZeroAndSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wcm_test_cache_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = CacheCleaner.Clear(tempDir);

            Assert.True(result.Success);
            Assert.Equal(0, result.FilesDeleted);
            Assert.Equal(0, result.DirectoriesDeleted);
            Assert.Equal(0, result.BytesFreed);
            Assert.True(Directory.Exists(tempDir));
            Assert.Empty(Directory.EnumerateFileSystemEntries(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Clear_WithFilesAndSubdirectories_DeletesAllAndReportsAccurately()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wcm_test_cache_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "favicons");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(tempDir, "image1.png");
        var file2 = Path.Combine(subDir, "icon.ico");
        File.WriteAllBytes(file1, new byte[100]);
        File.WriteAllBytes(file2, new byte[250]);

        try
        {
            var result = CacheCleaner.Clear(tempDir);

            Assert.True(result.Success);
            Assert.Equal(2, result.FilesDeleted);
            Assert.Equal(1, result.DirectoriesDeleted);
            Assert.Equal(350, result.BytesFreed);
            Assert.True(Directory.Exists(tempDir));
            Assert.Empty(Directory.EnumerateFileSystemEntries(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Clear_NonExistentDirectory_ReturnsSuccessWithZero()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "wcm_non_existent_" + Guid.NewGuid().ToString("N"));

        var result = CacheCleaner.Clear(nonExistent);

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.BytesFreed);
        Assert.True(Directory.Exists(nonExistent));

        Directory.Delete(nonExistent, recursive: true);
    }
}
