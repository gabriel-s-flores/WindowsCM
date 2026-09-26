// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Capture.Win32;

// Converts Win32 clipboard DIBs (CF_DIB/CF_DIBV5 payloads) to PNG bytes for
// hashed asset persistence. Supports 24bpp BI_RGB and 32bpp BI_RGB or
// BI_BITFIELDS with the standard BGR masks — the layout Windows uses when
// it synthesizes CF_DIB/CF_DIBV5 from a 32-bit bitmap (Print Screen, .NET
// Clipboard.SetImage). Without BI_BITFIELDS those copies were silently
// never captured (found by the real-app smoke). Paletted/compressed DIBs
// throw NotSupportedException.
public static class DibToPng
{
    private const int BI_RGB = 0;
    private const int BI_BITFIELDS = 3;
    private const int HeaderPeekLength = 52; // BITMAPINFOHEADER + the three masks

    // Bytes CanConvert needs from the start of the DIB.
    public static int HeaderLength => HeaderPeekLength;

    // Header-only check (the first 40 bytes): whether FromDib will accept
    // this DIB flavor. The clipboard reader uses it to pick a format while
    // the clipboard is open and runs the (slow) PNG encoding only after
    // closing it — encoding a 4K screenshot with the clipboard held took
    // ~0.5 s, during which every other app's copy and paste failed.
    public static bool CanConvert(ReadOnlySpan<byte> header)
    {
        if (header.Length < 40)
        {
            return false;
        }
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(14, 2));
        var compression = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(16, 4));
        if (headerSize < 40 || width <= 0 || height == 0 || height == int.MinValue
            || !ImageLimits.IsWithin(width, Math.Abs(height)))
        {
            return false;
        }
        return compression switch
        {
            BI_RGB => bitCount is 24 or 32,
            BI_BITFIELDS => bitCount == 32 && header.Length >= HeaderPeekLength && HasStandardMasks(header),
            _ => false,
        };
    }

    // Red/green/blue masks sit right after the 40-byte BITMAPINFOHEADER
    // (inside the header for V4/V5, just past it for a plain info header).
    private static bool HasStandardMasks(ReadOnlySpan<byte> dib) =>
        BinaryPrimitives.ReadUInt32LittleEndian(dib.Slice(40, 4)) == 0x00FF0000
        && BinaryPrimitives.ReadUInt32LittleEndian(dib.Slice(44, 4)) == 0x0000FF00
        && BinaryPrimitives.ReadUInt32LittleEndian(dib.Slice(48, 4)) == 0x000000FF;

    // The deferred half of the clipboard read: PNG bytes pass through, a
    // DIB is encoded; an unusable DIB yields no image instead of throwing.
    public static ImageSnapshot? ToSnapshot(byte[] bytes, bool isPng)
    {
        if (isPng)
        {
            if (bytes.Length == 0)
            {
                return null;
            }
            // Stored as is, but every thumbnail decodes it whole: a PNG over
            // the cap is skipped like a bitmap over it.
            if (ImageLimits.TryReadPngSize(bytes, out var pngWidth, out var pngHeight)
                && !ImageLimits.IsWithin(pngWidth, pngHeight))
            {
                return null;
            }
            return new ImageSnapshot("image/png", bytes);
        }
        try
        {
            return new ImageSnapshot("image/png", FromDib(bytes));
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException
            or OverflowException or IndexOutOfRangeException or OutOfMemoryException)
        {
            return null;
        }
    }

    public static byte[] FromDib(byte[] dib)
    {
        if (dib.Length < 40)
        {
            throw new ArgumentException("DIB too small for BITMAPINFOHEADER.", nameof(dib));
        }
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(0, 4));
        var width = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8, 4));
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14, 2));
        var compression = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(16, 4));
        if (headerSize < 40 || width <= 0 || height == 0 || height == int.MinValue)
        {
            throw new ArgumentException("Invalid DIB header.", nameof(dib));
        }
        var bitfields = compression == BI_BITFIELDS;
        if (bitfields && !(bitCount == 32 && dib.Length >= HeaderPeekLength && HasStandardMasks(dib)))
        {
            throw new NotSupportedException("Only standard BGR bitfield masks on 32bpp DIBs are supported.");
        }
        if (compression != BI_RGB && !bitfields)
        {
            throw new NotSupportedException($"Compressed DIBs unsupported (compression {compression}).");
        }
        if (bitCount is not (24 or 32))
        {
            throw new NotSupportedException($"DIB bit depth {bitCount}bpp unsupported in v1.");
        }
        var topDown = height < 0;
        var absHeight = Math.Abs(height);
        if (!ImageLimits.IsWithin(width, absHeight))
        {
            throw new NotSupportedException($"DIB of {width}x{absHeight} is over the {ImageLimits.MaxPixels} pixel cap.");
        }
        // Pixels start after the header, the masks of a plain info header
        // with BI_BITFIELDS, and any (optional) color table. Checked in
        // 64-bit before anything is allocated: the header is the source
        // app's word, and a 52-byte block claiming 20000 x 20000 used to
        // allocate 1.5 GB before failing.
        var colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(32, 4));
        var pixelOffset = (long)headerSize
            + (bitfields && headerSize == 40 ? 12 : 0)
            + (colorsUsed > 0 ? colorsUsed * 4L : 0);
        var stride = bitCount == 32 ? width * 4L : (width * 3L + 3) / 4 * 4;
        if (pixelOffset + stride * absHeight > dib.Length)
        {
            throw new ArgumentException("DIB pixel data truncated.", nameof(dib));
        }
        return bitCount == 32
            ? From32bpp(dib, (int)pixelOffset, width, absHeight, topDown)
            : From24bpp(dib, (int)pixelOffset, width, absHeight, topDown);
    }

    // Rows go straight from the DIB into the compressor: no full-size RGBA
    // copy and no second copy with the filter bytes.
    private static byte[] From32bpp(byte[] dib, int pixelOffset, int width, int height, bool topDown)
    {
        var stride = width * 4;
        return PngEncoder.Encode(width, height, bytesPerPixel: 4, colorType: 6, (row, dest) =>
        {
            var s = pixelOffset + (topDown ? row : height - 1 - row) * stride;
            for (var d = 0; d < dest.Length; d += 4, s += 4)
            {
                dest[d] = dib[s + 2]; // R
                dest[d + 1] = dib[s + 1]; // G
                dest[d + 2] = dib[s]; // B
                dest[d + 3] = 255; // DIB has no alpha: opaque
            }
        });
    }

    private static byte[] From24bpp(byte[] dib, int pixelOffset, int width, int height, bool topDown)
    {
        var stride = (width * 3 + 3) / 4 * 4;
        return PngEncoder.Encode(width, height, bytesPerPixel: 3, colorType: 2, (row, dest) =>
        {
            var s = pixelOffset + (topDown ? row : height - 1 - row) * stride;
            for (var d = 0; d < dest.Length; d += 3, s += 3)
            {
                dest[d] = dib[s + 2];
                dest[d + 1] = dib[s + 1];
                dest[d + 2] = dib[s];
            }
        });
    }
}

// Minimal PNG writer (truecolor, filter 0, zlib via ZLibStream). Enough for
// clipboard screenshots; not a general encoder.
internal static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    // Fills one row (top to bottom) of unfiltered pixels.
    public delegate void RowFiller(int row, Span<byte> destination);

    public static byte[] Encode(int width, int height, int bytesPerPixel, byte colorType, RowFiller fillRow)
    {
        using var output = new MemoryStream();
        output.Write(Signature);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = colorType;
        WriteChunk(output, "IHDR", ihdr);

        var line = new byte[1 + width * bytesPerPixel]; // [0] = filter: none
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var row = 0; row < height; row++)
            {
                fillRow(row, line.AsSpan(1));
                zlib.Write(line);
            }
        }
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        output.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc32.Of(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = Build();

    private static uint[] Build()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }

    public static uint Of(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
