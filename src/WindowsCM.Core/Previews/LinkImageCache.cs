// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Previews;

// Thumbnail bytes keyed by page-URL hash (Copyous getCachePath + MD5(url)
// parity). One file per link item: <cache-dir>/<md5(url)>. A hit skips the
// og:image download; offline still yields no preview (service-owned rule).
public interface ILinkImageCache
{
    string Directory { get; }
    string? TryGet(string url);
    string SaveIfAbsent(string url, byte[] bytes);
}

public sealed class LinkImageCache : ILinkImageCache
{
    public string Directory { get; }

    public LinkImageCache(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    public static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsCM",
        "Cache",
        "link-images");

    public static string FileNameFor(string url) => ClipboardHash.Md5Hex(url);

    public string? TryGet(string url)
    {
        var path = Path.Combine(Directory, FileNameFor(url));
        return File.Exists(path) ? path : null;
    }

    public string SaveIfAbsent(string url, byte[] bytes)
    {
        var path = Path.Combine(Directory, FileNameFor(url));
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, bytes);
        }
        return path;
    }

    public void SweepOrphans(IEnumerable<string> referencedUrls)
    {
        var keep = new HashSet<string>(
            referencedUrls.Select(FileNameFor),
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
