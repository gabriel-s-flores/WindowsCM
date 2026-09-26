// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Paste;

// Reads persisted image bytes back for copy-back. Best effort like the
// orphan sweep: unreadable files report missing (null) instead of throwing.
public sealed class FileImageReader : IImageFileReader
{
    private readonly string _directory;

    public FileImageReader(string directory)
    {
        _directory = directory;
    }

    public byte[]? LoadPng(string content)
    {
        var name = FileUris.TryGetFileName(content);
        if (name is null)
        {
            return null;
        }
        var path = IncognitoPath(content) ?? Path.Combine(_directory, name);
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // An image captured in incognito lives in its session's temp folder
    // (EphemeralImageAssetStore), not in the persistent one: pasting it
    // back used to report it missing. Only such a folder is honored.
    private static string? IncognitoPath(string content)
    {
        try
        {
            var path = new Uri(content).LocalPath;
            var folder = Path.GetFileName(Path.GetDirectoryName(path));
            return folder is not null && folder.StartsWith(EphemeralImageAssetStore.DirectoryPrefix, StringComparison.Ordinal)
                ? path
                : null;
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
