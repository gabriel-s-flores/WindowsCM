// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

public sealed record CacheCleanResult(
    int FilesDeleted,
    int DirectoriesDeleted,
    long BytesFreed,
    bool Success,
    string? ErrorMessage = null);

// Deep module for safe cache clearing (research 04/05 parity).
// Enumerates files, calculates freed space, handles locked files gracefully,
// and ensures the cache root exists and is left ready for subsequent caching.
public static class CacheCleaner
{
    public static CacheCleanResult Clear(string cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            return new CacheCleanResult(0, 0, 0, false, "Diretório de cache não especificado.");
        }

        if (!Directory.Exists(cacheDirectory))
        {
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                return new CacheCleanResult(0, 0, 0, true);
            }
            catch (Exception ex)
            {
                return new CacheCleanResult(0, 0, 0, false, ex.Message);
            }
        }

        int filesDeleted = 0;
        int dirsDeleted = 0;
        long bytesFreed = 0;
        string? errorMessage = null;

        try
        {
            var dirInfo = new DirectoryInfo(cacheDirectory);

            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    var len = file.Length;
                    file.Delete();
                    bytesFreed += len;
                    filesDeleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errorMessage ??= ex.Message;
                }
            }

            foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    subDir.Delete(recursive: true);
                    dirsDeleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errorMessage ??= ex.Message;
                }
            }

            return new CacheCleanResult(filesDeleted, dirsDeleted, bytesFreed, true, errorMessage);
        }
        catch (Exception ex)
        {
            return new CacheCleanResult(filesDeleted, dirsDeleted, bytesFreed, false, ex.Message);
        }
    }
}
