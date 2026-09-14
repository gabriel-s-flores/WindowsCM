// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

// Ephemeral image store for incognito sessions.
// All image assets are saved to an isolated temporary folder.
// Upon disposal, the entire directory is deleted from disk with zero traces left.
public sealed class EphemeralImageAssetStore : IImageAssetStore, IDisposable
{
    public string Directory { get; }
    private bool _disposed;

    // The uninstaller sweeps %TEMP% for this prefix (InstallerContract).
    public const string DirectoryPrefix = "WindowsCM_Incognito_";

    public EphemeralImageAssetStore(string? baseDir = null)
    {
        var root = baseDir ?? Path.GetTempPath();
        Directory = Path.Combine(root, DirectoryPrefix + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string SaveIfAbsent(string fileName, byte[] bytes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EphemeralImageAssetStore));
        }
        var safe = Path.GetFileName(fileName);
        var path = Path.Combine(Directory, safe);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, bytes);
        }
        return FileUris.FromPath(path);
    }

    public void SweepOrphans(IEnumerable<string> referencedFileNames)
    {
        if (_disposed || !System.IO.Directory.Exists(Directory))
        {
            return;
        }
        var keep = new HashSet<string>(
            referencedFileNames.Select(Path.GetFileName).OfType<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory))
        {
            if (!keep.Contains(Path.GetFileName(file)))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (System.IO.Directory.Exists(Directory))
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch { }
        }
    }
}
