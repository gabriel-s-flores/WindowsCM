// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;
using WindowsCM.Core.Classification;

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
                // Over the cap the writer offers the PNG format only,
                // instead of a multi-GB bitmap built on the UI thread.
                if (!ImageLimits.IsWithin(width, height))
                {
                    throw new NotSupportedException($"PNG of {width}x{height} is over the {ImageLimits.MaxPixels} pixel cap.");
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

    // Exactly the declared size: a growing stream doubled its buffer up to
    // twice the image, and a stream that inflates past it (a zip bomb)
    // stops at the first extra byte.
    private static byte[] Inflate(byte[] deflated, int expected)
    {
        using var input = new MemoryStream(deflated);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var output = new byte[expected];
        try
        {
            zlib.ReadExactly(output);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
        {
            throw new ArgumentException("PNG pixel data has an unexpected size.", nameof(deflated), ex);
        }
        if (zlib.ReadByte() != -1)
        {
            throw new ArgumentException("PNG pixel data has an unexpected size.", nameof(deflated));
        }
        return output;
    }

    // Unfilters row by row straight into the bottom-up bitmap: no full-size
    // intermediate copy, no byte-at-a-time writer (a 4K image allocated
    // ~255 MB on the UI thread).
    private static byte[] ToDib(byte[] filtered, int width, int height, int bytesPerPixel)
    {
        var lineLength = width * bytesPerPixel;
        var stride = 1 + lineLength;
        var dib = new byte[40 + width * height * 4];
        var header = dib.AsSpan(0, 40);
        BinaryPrimitives.WriteInt32LittleEndian(header, 40); // header size
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height); // positive: bottom-up
        BinaryPrimitives.WriteInt16LittleEndian(header[12..], 1); // planes
        BinaryPrimitives.WriteInt16LittleEndian(header[14..], 32); // 32bpp BI_RGB (alpha ignored by readers)
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], width * height * 4); // image size
        var line = new byte[lineLength];
        var prior = new byte[lineLength];
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(filtered, row * stride + 1, line, 0, lineLength);
            Unfilter(filtered[row * stride], line, prior, bytesPerPixel);
            var d = 40 + (height - 1 - row) * width * 4;
            for (var s = 0; s < lineLength; s += bytesPerPixel, d += 4)
            {
                dib[d] = line[s + 2]; // B
                dib[d + 1] = line[s + 1]; // G
                dib[d + 2] = line[s]; // R
                dib[d + 3] = bytesPerPixel == 4 ? line[s + 3] : (byte)255; // X
            }
            (prior, line) = (line, prior);
        }
        return dib;
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
