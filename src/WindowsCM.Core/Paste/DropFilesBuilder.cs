// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Text;

namespace WindowsCM.Core.Paste;

// Builds the CF_HDROP payload for file copy-back (research 02 § "Regras de
// ownership" 8): DROPFILES with pFiles = header size, fWide = TRUE, absolute
// paths as UTF-16 double-NUL-terminated. The drop effect is always copy —
// Copyous parity forces the copy operation on copy-back, so a stored cut
// marker never reaches the clipboard.
public static class DropFilesBuilder
{
    // DROPEFFECT_COPY (research 02: MOVE would mean cut).
    public const int CopyEffect = 1;

    public static byte[] Build(IReadOnlyList<string> localPaths)
    {
        if (localPaths.Count == 0)
        {
            throw new ArgumentException("At least one path is required.", nameof(localPaths));
        }
        var text = string.Join("\0", localPaths) + "\0\0";
        var encoded = Encoding.Unicode.GetBytes(text);
        var payload = new byte[20 + encoded.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 20); // pFiles
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), 0); // pt.x
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), 0); // pt.y
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12, 4), 0); // fNC
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16, 4), 1); // fWide
        encoded.CopyTo(payload, 20);
        return payload;
    }
}
