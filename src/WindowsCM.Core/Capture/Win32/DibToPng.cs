// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;

namespace WindowsCM.Core.Capture.Win32;

// Converts Win32 clipboard DIBs (CF_DIB/CF_DIBV5 payloads) to PNG bytes for
// hashed asset persistence. v1 supports 32bpp and 24bpp BI_RGB only — the
// screenshot/clipboard common cases; paletted/compressed DIBs throw
// NotSupportedException (manual smoke covers them, richer support on demand).
public static class DibToPng
{
    private const int BI_RGB = 0;

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
        if (headerSize < 40 || width <= 0 || height == 0)
        {
            throw new ArgumentException("Invalid DIB header.", nameof(dib));
        }
        if (compression != BI_RGB)
        {
            throw new NotSupportedException($"Compressed DIBs unsupported (compression {compression}).");
        }
        var topDown = height < 0;
        var absHeight = Math.Abs(height);
        return bitCount switch
        {
            32 => From32bpp(dib, headerSize, width, absHeight, topDown),
            24 => From24bpp(dib, headerSize, width, absHeight, topDown),
            _ => throw new NotSupportedException($"DIB bit depth {bitCount}bpp unsupported in v1."),
        };
    }

    private static byte[] From32bpp(byte[] dib, int headerSize, int width, int height, bool topDown)
    {
        var stride = width * 4;
        var rgba = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            var srcRow = topDown ? row : height - 1 - row;
            var srcOff = headerSize + srcRow * stride;
            if (srcOff + stride > dib.Length)
            {
                throw new ArgumentException("DIB pixel data truncated.", nameof(dib));
            }
            for (var col = 0; col < width; col++)
            {
                var s = srcOff + col * 4;
                var d = (row * width + col) * 4;
                rgba[d] = dib[s + 2]; // R
                rgba[d + 1] = dib[s + 1]; // G
                rgba[d + 2] = dib[s]; // B
                rgba[d + 3] = 255; // DIB has no alpha: opaque
            }
        }
        return PngEncoder.EncodeRgba(width, height, rgba);
    }

    private static byte[] From24bpp(byte[] dib, int headerSize, int width, int height, bool topDown)
    {
        var stride = ((width * 3 + 3) / 4) * 4;
        var rgb = new byte[width * height * 3];
        for (var row = 0; row < height; row++)
        {
            var srcRow = topDown ? row : height - 1 - row;
            var srcOff = headerSize + srcRow * stride;
            if (srcOff + stride > dib.Length)
            {
                throw new ArgumentException("DIB pixel data truncated.", nameof(dib));
            }
            for (var col = 0; col < width; col++)
            {
                var s = srcOff + col * 3;
                var d = (row * width + col) * 3;
                rgb[d] = dib[s + 2];
                rgb[d + 1] = dib[s + 1];
                rgb[d + 2] = dib[s];
            }
        }
        return PngEncoder.EncodeRgb(width, height, rgb);
    }
}

// Minimal PNG writer (truecolor, filter 0, zlib via ZLibStream). Enough for
// clipboard screenshots; not a general encoder.
internal static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeRgba(int width, int height, byte[] rgba) =>
        Encode(width, height, rgba, bytesPerPixel: 4, colorType: 6);

    public static byte[] EncodeRgb(int width, int height, byte[] rgb) =>
        Encode(width, height, rgb, bytesPerPixel: 3, colorType: 2);

    private static byte[] Encode(int width, int height, byte[] pixels, int bytesPerPixel, byte colorType)
    {
        using var output = new MemoryStream();
        output.Write(Signature);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = colorType;
        WriteChunk(output, "IHDR", ihdr);

        using var raw = new MemoryStream();
        var stride = width * bytesPerPixel;
        for (var row = 0; row < height; row++)
        {
            raw.WriteByte(0); // filter: none
            raw.Write(pixels, row * stride, stride);
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
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
