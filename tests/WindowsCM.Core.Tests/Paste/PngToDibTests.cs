// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;
using WindowsCM.Core.Capture.Win32;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

public sealed class PngToDibTests
{
    private static byte[] Dib32(int width, int height, Func<int, int, (byte B, byte G, byte R, byte A)> pixel)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(width); w.Write(height);
        w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(width * height * 4);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (var row = height - 1; row >= 0; row--)
        {
            for (var col = 0; col < width; col++)
            {
                var (b, g, r, a) = pixel(col, row);
                w.Write(b); w.Write(g); w.Write(r); w.Write(a);
            }
        }
        return ms.ToArray();
    }

    private static (byte B, byte G, byte R) DibPixel(byte[] dib, int width, int col, int rowFromTop)
    {
        // Source DIBs here are bottom-up: flip rows.
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8, 4));
        var off = 40 + ((height - 1 - rowFromTop) * width * 4) + col * 4;
        return (dib[off], dib[off + 1], dib[off + 2]);
    }

    private static (byte B, byte G, byte R) OutPixel(byte[] dib, int width, int col, int rowFromTop)
    {
        // PngToDib always emits bottom-up 32bpp BI_RGB.
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8, 4));
        var off = 40 + ((height - 1 - rowFromTop) * width * 4) + col * 4;
        return (dib[off], dib[off + 1], dib[off + 2]);
    }

    [Fact]
    public void FromPng_32bppRoundTrip_PreservesPixels()
    {
        var dib = Dib32(2, 2, (c, r) => ((byte)(c * 10), (byte)(r * 20), 200, 255));
        var png = DibToPng.FromDib(dib);

        var back = PngToDib.FromPng(png);

        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(back.AsSpan(16, 4))); // BI_RGB
        for (var r = 0; r < 2; r++)
        {
            for (var c = 0; c < 2; c++)
            {
                Assert.Equal(DibPixel(dib, 2, c, r), OutPixel(back, 2, c, r));
            }
        }
    }

    [Fact]
    public void FromPng_24bppRoundTrip_PreservesPixels()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(2); w.Write(2);
        w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(16);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        // Bottom-up rows, 3 bytes per pixel + 2 padding per row (stride 8).
        w.Write(new byte[] { 1, 2, 3, 4, 5, 6, 0, 0 });
        w.Write(new byte[] { 7, 8, 9, 10, 11, 12, 0, 0 });
        var png = DibToPng.FromDib(ms.ToArray());

        var back = PngToDib.FromPng(png);

        Assert.Equal(32, BinaryPrimitives.ReadInt16LittleEndian(back.AsSpan(14, 2)));
        Assert.Equal((1, 2, 3), OutPixel(back, 2, 0, 1));
        Assert.Equal((4, 5, 6), OutPixel(back, 2, 1, 1));
        Assert.Equal((7, 8, 9), OutPixel(back, 2, 0, 0));
        Assert.Equal((10, 11, 12), OutPixel(back, 2, 1, 0));
    }

    [Theory]
    [InlineData(1)] // Sub
    [InlineData(2)] // Up
    [InlineData(3)] // Average
    [InlineData(4)] // Paeth
    public void FromPng_FilteredScanlines_DecodeToSamePixels(byte filter)
    {
        // 2x1 RGBA: row bytes before filtering are known; each filter type
        // must unfilter back to them.
        var raw = new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 };
        var filtered = ApplyFilter(filter, raw, bytesPerPixel: 4);
        var png = BuildPng(2, 1, colorType: 6, FilteredRow(filter, filtered));

        var dib = PngToDib.FromPng(png);

        Assert.Equal((30, 20, 10), OutPixel(dib, 2, 0, 0)); // stored RGBA→BGR
        Assert.Equal((60, 50, 40), OutPixel(dib, 2, 1, 0));
    }

    [Fact]
    public void FromPng_BadSignature_Throws()
    {
        Assert.Throws<ArgumentException>(() => PngToDib.FromPng([1, 2, 3]));
    }

    [Fact]
    public void FromPng_PalettePng_ThrowsNotSupported()
    {
        // Color type 3 (palette): outside the v1 screenshot scope.
        var png = BuildPng(1, 1, colorType: 3, [0, 0]);

        Assert.Throws<NotSupportedException>(() => PngToDib.FromPng(png));
    }

    // --- Minimal test-local PNG builder (filtered rows in, bytes out) ---

    private static byte[] FilteredRow(byte filter, byte[] row)
    {
        var out_ = new byte[row.Length + 1];
        out_[0] = filter;
        row.CopyTo(out_, 1);
        return out_;
    }

    private static byte[] ApplyFilter(byte filter, byte[] raw, int bytesPerPixel)
    {
        // Single row, so "prior" is zeros; encodes raw with the given filter.
        var enc = new byte[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var a = i >= bytesPerPixel ? raw[i - bytesPerPixel] : 0;
            var b = 0; // no prior row
            var c = 0;
            enc[i] = filter switch
            {
                0 => raw[i],
                1 => (byte)(raw[i] - a),
                2 => (byte)(raw[i] - b),
                3 => (byte)(raw[i] - ((a + b) >> 1)),
                4 => (byte)(raw[i] - Paeth(a, b, c)),
                _ => throw new ArgumentOutOfRangeException(nameof(filter)),
            };
        }
        return enc;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] BuildPng(int width, int height, byte colorType, byte[] filteredRows)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; ihdr[9] = colorType; // bit depth 8
        WriteChunk(ms, "IHDR", ihdr);
        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(filteredRows);
        }
        WriteChunk(ms, "IDAT", comp.ToArray());
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream out_, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        out_.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        out_.Write(typeBytes);
        out_.Write(data);
        var crc = Crc(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        out_.Write(crcBytes);
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        uint[] table = CrcTable.Value;
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        foreach (var b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly Lazy<uint[]> CrcTable = new(() =>
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
    });
}
