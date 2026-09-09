// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace WindowsCM.Core.Classification;

// Content hashes for own-copy suppression and history dedup (Copyous
// parity: text hashes the raw text, files hash the stripped paths).
public static class ClipboardHash
{
    public static string Md5Hex(byte[] bytes) =>
        Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    public static string Md5Hex(string text) =>
        Md5Hex(Encoding.UTF8.GetBytes(text));

    // Copyous hashes `paths without file:// joined by \n` after URI-decoding.
    public static string FileHash(IEnumerable<string> uris) =>
        Md5Hex(string.Join("\n", uris.Select(StripFileScheme)));

    private static string StripFileScheme(string uri)
    {
        var path = uri.StartsWith("file://", StringComparison.Ordinal)
            ? uri["file://".Length..]
            : uri;
        return Uri.UnescapeDataString(path);
    }
}
