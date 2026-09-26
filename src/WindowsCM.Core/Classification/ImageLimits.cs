// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;

namespace WindowsCM.Core.Classification;

// Largest bitmap captured, or converted for paste-back: 8192 x 8192 (67 MP).
// An 8K screen (33 MP) or three 4K monitors side by side fit. A 12k x 12k
// canvas copied from an editor peaked near 2.5 GB and 20 s of CPU in the
// tray process, and every thumbnail of it decoded the whole image again.
public static class ImageLimits
{
    public const long MaxPixels = 8192L * 8192L;

    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool IsWithin(long width, long height) =>
        width > 0 && height > 0 && width * height <= MaxPixels;

    // Width and height from a PNG's IHDR (always the first chunk).
    public static bool TryReadPngSize(ReadOnlySpan<byte> png, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (png.Length < 24 || !png[..8].SequenceEqual(PngSignature) || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }
        width = BinaryPrimitives.ReadInt32BigEndian(png.Slice(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(png.Slice(20, 4));
        return true;
    }
}
