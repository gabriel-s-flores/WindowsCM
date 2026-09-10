// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace WindowsCM.Core.Classification;

// Content hashes for own-copy suppression and history dedup (Copyous
// parity). The hash always agrees with the stored content: files are
// canonicalized to local paths first, so equivalent URI spellings hash
// (and store) identically and UNIQUE(type,content) can never disagree
// with the hash.
public static class ClipboardHash
{
    public static string Md5Hex(byte[] bytes) =>
        Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    public static string Md5Hex(string text) =>
        Md5Hex(Encoding.UTF8.GetBytes(text));

    // Copyous hashes `paths without file:// joined by \n` after URI-decoding.
    // file:// URIs become real local paths (drive/UNC-aware via LocalPath,
    // so paste receives usable paths); anything else stays verbatim.
    public static string FileHash(IEnumerable<string> uris) =>
        Md5Hex(string.Join("\n", uris.Select(ToLocalPath)));

    public static string ToLocalPath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
        {
            return parsed.LocalPath;
        }
        var path = uri.StartsWith("file://", StringComparison.Ordinal)
            ? uri["file://".Length..]
            : uri;
        return Uri.UnescapeDataString(path);
    }
}
