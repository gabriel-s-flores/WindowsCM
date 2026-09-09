// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Classification;

// A raw image payload observed on the clipboard.
public sealed record ImageSnapshot(string MimeType, byte[] Data);

// Image naming (Copyous parity: `<md5>.<ext-do-mimetype>`).
public static class ImageContent
{
    public static string ExtensionFor(string mimeType)
    {
        var parts = mimeType.Split('/');
        return parts.Length > 1 && parts[1].Length != 0 ? parts[1] : "bin";
    }
}
