// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;

namespace WindowsCM.Core.Paste;

// Reverse of DibToPng (which only emits what this reads): PNG bytes to a
// CF_DIB payload (BITMAPINFOHEADER + bottom-up 32bpp BI_RGB) for clipboard
// write-back. v1 scope mirrors the encoder — 8-bit truecolor (RGB/RGBA),
// non-interlaced, all five filter types. Anything else (palette, gray,
// 16-bit, interlaced) throws NotSupportedException and the writer falls
// back to the PNG clipboard format; malformed input throws ArgumentException.
public static class PngToDib
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] FromPng(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new ArgumentException("Not a PNG file.", nameof(png));
        }
        var offset = 8;
        int width = 0, height = 0, bytesPerPixel = 0;
        var seenIhdr = false;
        var idat = new MemoryStream();
        while (offset + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            offset += 8;
            if (length < 0 || length > png.Length - offset - 4)
            {
                throw new ArgumentException("Truncated PNG chunk.", nameof(png));
            }
            if (type == "IHDR")
            {
                if (seenIhdr || length != 13)
                {
                    throw new ArgumentException("Invalid PNG header.", nameof(png));
                }
                seenIhdr = true;
                width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset + 4, 4));
                var bitDepth = png[offset + 8];
                var colorType = png[offset + 9];
                if (png[offset + 10] != 0 || png[offset + 11] != 0)
                {
                    throw new NotSupportedException("Only standard PNG compression/filter methods are supported.");
                }
                if (png[offset + 12] != 0)
                {
                    throw new NotSupportedException("Interlaced PNGs are unsupported in v1.");
                }
                if (bitDepth != 8)
                {
                    throw new NotSupportedException($"PNG bit depth {bitDepth} unsupported in v1.");
                }
                bytesPerPixel = colorType switch
                {
                    2 => 3,
                    6 => 4,
                    _ => throw new NotSupportedException($"PNG color type {colorType} unsupported in v1."),
                };
                if (width <= 0 || height <= 0)
                {
                    throw new ArgumentException("Invalid PNG dimensions.", nameof(png));
                }
            }
            else if (type == "IDAT")
            {
                if (!seenIhdr)
                {
                    throw new ArgumentException("IDAT before IHDR.", nameof(png));
                }
                idat.Write(png, offset, length);
            }
            else if (type == "IEND")
            {
                break;
            }
            // Ancillary chunks (tEXt, pHYs, …) are skipped.
            offset += length + 4; // data + CRC
        }
        if (!seenIhdr || idat.Length == 0)
        {
            throw new ArgumentException("PNG has no image data.", nameof(png));
        }
        return ToDib(Inflate(idat.ToArray(), height * (1 + width * bytesPerPixel)), width, height, bytesPerPixel);
    }

    private static byte[] Inflate(byte[] deflated, int expected)
    {
        using var input = new MemoryStream(deflated);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        if (output.Length != expected)
        {
            throw new ArgumentException("PNG pixel data has an unexpected size.", nameof(deflated));
        }
        return output.ToArray();
    }

    private static byte[] ToDib(byte[] filtered, int width, int height, int bytesPerPixel)
    {
        var stride = 1 + width * bytesPerPixel;
        var raw = new byte[width * height * bytesPerPixel];
        var prior = new byte[width * bytesPerPixel];
        for (var row = 0; row < height; row++)
        {
            var filter = filtered[row * stride];
            var line = new byte[width * bytesPerPixel];
            Buffer.BlockCopy(filtered, row * stride + 1, line, 0, line.Length);
            Unfilter(filter, line, prior, bytesPerPixel);
            Buffer.BlockCopy(line, 0, raw, row * line.Length, line.Length);
            prior = line;
        }
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); // header size
        w.Write(width);
        w.Write(height); // positive: bottom-up
        w.Write((short)1); // planes
        w.Write((short)32); // 32bpp BI_RGB (alpha channel ignored by readers)
        w.Write(0); // BI_RGB
        w.Write(width * height * 4); // image size
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (var row = height - 1; row >= 0; row--)
        {
            for (var col = 0; col < width; col++)
            {
                var s = (row * width + col) * bytesPerPixel;
                w.Write(raw[s + 2]); // B
                w.Write(raw[s + 1]); // G
                w.Write(raw[s]); // R
                w.Write(bytesPerPixel == 4 ? raw[s + 3] : (byte)255); // X
            }
        }
        return ms.ToArray();
    }

    private static void Unfilter(byte filter, byte[] line, byte[] prior, int bytesPerPixel)
    {
        for (var i = 0; i < line.Length; i++)
        {
            var a = i >= bytesPerPixel ? line[i - bytesPerPixel] : 0;
            var b = prior[i];
            var c = i >= bytesPerPixel ? prior[i - bytesPerPixel] : 0;
            var recon = filter switch
            {
                0 => line[i],
                1 => line[i] + a,
                2 => line[i] + b,
                3 => line[i] + ((a + b) >> 1),
                4 => line[i] + Paeth(a, b, c),
                _ => throw new ArgumentException($"Unknown PNG filter {filter}."),
            };
            line[i] = (byte)recon;
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
