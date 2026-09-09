// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

// Image bytes live under <LocalAppData>/WindowsCM/images/<md5>.<ext>
// (Copyous getImagesPath parity). The DB content is the file:// URI.
public interface IImageAssetStore
{
    string Directory { get; }
    string SaveIfAbsent(string fileName, byte[] bytes);
    void SweepOrphans(IEnumerable<string> referencedFileNames);
}

public static class FileUris
{
    public static string FromPath(string path) => new Uri(path).AbsoluteUri;

    // Maps a stored Image content (file:// URI) back to its file name for
    // orphan sweeps. Null when the content is not a file URI.
    public static string? TryGetFileName(string content)
    {
        if (!content.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        try
        {
            return Path.GetFileName(new Uri(content).LocalPath);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}

public sealed class FileImageAssetStore : IImageAssetStore
{
    public string Directory { get; }

    public FileImageAssetStore(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    public string SaveIfAbsent(string fileName, byte[] bytes)
    {
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
                catch (IOException)
                {
                    // Best effort: a locked file is retried on next startup.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best effort: ACL-denied files are retried on next startup.
                }
            }
        }
    }
}
