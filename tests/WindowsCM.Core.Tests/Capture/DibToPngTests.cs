// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture.Win32;

namespace WindowsCM.Core.Tests.Capture;

public sealed class DibToPngTests
{
    // Minimal 1x1 32bpp BI_RGB DIB: BITMAPINFOHEADER (40 bytes) + 1 BGRA pixel.
    private static byte[] DIB1x1(byte b, byte g, byte r)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); // header size
        w.Write(1); // width
        w.Write(1); // height (bottom-up)
        w.Write((short)1); // planes
        w.Write((short)32); // bit count
        w.Write(0); // BI_RGB
        w.Write(4); // image size
        w.Write(0); w.Write(0); w.Write(0); w.Write(0); // XPels/YPels/clrUsed/clrImportant
        w.Write(b); w.Write(g); w.Write(r); w.Write((byte)0); // BGRA
        return ms.ToArray();
    }

    [Fact]
    public void FromDib_1x1Red_ProducesPngSignatureAndDimensions()
    {
        var png = DibToPng.FromDib(DIB1x1(b: 0, g: 0, r: 255));

        // PNG signature.
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            png[..8]);
        // IHDR width/height big-endian at offsets 16/20.
        Assert.Equal(1, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(1, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Fact]
    public void FromDib_UnsupportedBitCount_Throws()
    {
        var dib = DIB1x1(0, 0, 0);
        // Patch bit count to 8bpp (paletted, unsupported in v1).
        dib[14] = 8; dib[15] = 0;

        Assert.Throws<NotSupportedException>(() => DibToPng.FromDib(dib));
    }

    [Fact]
    public void CanConvert_AcceptsExactlyWhatFromDibEncodes()
    {
        var rgb32 = DIB1x1(1, 2, 3);
        var rgb24 = DIB1x1(1, 2, 3);
        rgb24[14] = 24;
        var paletted = DIB1x1(1, 2, 3);
        paletted[14] = 8;
        var oddMasks = Bitfields2x1(0x000000FF, 0x0000FF00, 0x00FF0000);

        Assert.True(DibToPng.CanConvert(rgb32.AsSpan(0, 40)));
        Assert.True(DibToPng.CanConvert(rgb24.AsSpan(0, 40)));
        Assert.True(DibToPng.CanConvert(Bitfields2x1().AsSpan(0, DibToPng.HeaderLength)));
        Assert.True(DibToPng.CanConvert(V5Bitfields1x1().AsSpan(0, DibToPng.HeaderLength)));
        Assert.False(DibToPng.CanConvert(paletted.AsSpan(0, 40)));
        Assert.False(DibToPng.CanConvert(oddMasks.AsSpan(0, DibToPng.HeaderLength)));
        Assert.False(DibToPng.CanConvert(Bitfields2x1().AsSpan(0, 40))); // masks not visible
        Assert.False(DibToPng.CanConvert(rgb32.AsSpan(0, 20)));
        Assert.Throws<NotSupportedException>(() => DibToPng.FromDib(oddMasks));
    }

    // 2x1 32bpp BI_BITFIELDS with a plain BITMAPINFOHEADER: the masks follow
    // the header, then the pixels (blue, red) — how Windows synthesizes
    // CF_DIB from a 32-bit bitmap.
    private static byte[] Bitfields2x1(uint r = 0x00FF0000, uint g = 0x0000FF00, uint b = 0x000000FF)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(2); w.Write(1); w.Write((short)1); w.Write((short)32);
        w.Write(3); // BI_BITFIELDS
        w.Write(8); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        w.Write(r); w.Write(g); w.Write(b);
        w.Write((byte)255); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // blue
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)255); w.Write((byte)0); // red
        return ms.ToArray();
    }

    // 1x1 32bpp BI_BITFIELDS with a BITMAPV5HEADER (CF_DIBV5): the masks live
    // inside the 124-byte header and the pixel follows it.
    private static byte[] V5Bitfields1x1()
    {
        var dib = new byte[124 + 4];
        BitConverter.GetBytes(124).CopyTo(dib, 0);
        BitConverter.GetBytes(1).CopyTo(dib, 4);
        BitConverter.GetBytes(1).CopyTo(dib, 8);
        BitConverter.GetBytes((short)1).CopyTo(dib, 12);
        BitConverter.GetBytes((short)32).CopyTo(dib, 14);
        BitConverter.GetBytes(3).CopyTo(dib, 16);
        BitConverter.GetBytes(0x00FF0000u).CopyTo(dib, 40);
        BitConverter.GetBytes(0x0000FF00u).CopyTo(dib, 44);
        BitConverter.GetBytes(0x000000FFu).CopyTo(dib, 48);
        BitConverter.GetBytes(0xFF000000u).CopyTo(dib, 52);
        dib[124] = 0; dib[125] = 255; dib[126] = 0; dib[127] = 255; // green
        return dib;
    }

    [Fact]
    public void FromDib_Bitfields_ReadsPixelsAfterTheMasks()
    {
        var png = Decode(DibToPng.FromDib(Bitfields2x1()));

        Assert.Equal(2, png.Width);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, png.Rgba); // blue, red
    }

    [Fact]
    public void FromDib_V5Bitfields_ReadsPixelsAfterTheV5Header()
    {
        var png = Decode(DibToPng.FromDib(V5Bitfields1x1()));

        Assert.Equal(new byte[] { 0, 255, 0, 255 }, png.Rgba); // green
    }

    // Minimal reader for the encoder's own output: IHDR size + the single
    // unfiltered IDAT stream.
    private static (int Width, byte[] Rgba) Decode(byte[] png)
    {
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var idatLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
        using var zlib = new System.IO.Compression.ZLibStream(
            new MemoryStream(png, 41, idatLength), System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var bytes = raw.ToArray();
        var stride = width * 4;
        var rgba = new List<byte>();
        for (var row = 0; row * (stride + 1) < bytes.Length; row++)
        {
            rgba.AddRange(bytes.Skip(row * (stride + 1) + 1).Take(stride)); // skip the filter byte
        }
        return (width, rgba.ToArray());
    }

    [Fact]
    public void ToSnapshot_EncodesDibsPassesPngAndNeverThrows()
    {
        var fromDib = DibToPng.ToSnapshot(DIB1x1(0, 0, 255), isPng: false);
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2 };
        var passthrough = DibToPng.ToSnapshot(png, isPng: true);
        var truncated = DIB1x1(0, 0, 255);
        truncated[4] = 50; // claims 50 px wide: pixel data too short

        Assert.Equal("image/png", fromDib?.MimeType);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, fromDib?.Data[..8]);
        Assert.Same(png, passthrough?.Data);
        Assert.Null(DibToPng.ToSnapshot(truncated, isPng: false));
        Assert.Null(DibToPng.ToSnapshot([], isPng: true));
    }

    // A 40-byte header claiming width x height, 32bpp BI_RGB, and nothing
    // behind it (a buggy or hostile source app).
    private static byte[] HeaderOnly(int width, int height, int colorsUsed = 0)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(width); w.Write(height);
        w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(0);
        w.Write(0); w.Write(0); w.Write(colorsUsed); w.Write(0);
        w.Write(new byte[12]);
        return ms.ToArray();
    }

    // It used to allocate the full pixel buffer (1.5 GB for 20000 x 20000)
    // before noticing the data was not there.
    [Fact]
    public void ToSnapshot_HugeClaimOnATinyBuffer_IsRejectedWithoutAllocating()
    {
        var before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Null(DibToPng.ToSnapshot(HeaderOnly(20_000, 20_000), isPng: false));

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1_000_000);
    }

    [Theory]
    [InlineData(30_000, 30_000, 0)]          // width*height*4 overflows int
    [InlineData(1, int.MinValue, 0)]         // Math.Abs(int.MinValue) throws
    [InlineData(1, 1, int.MaxValue)]         // color table offset overflows
    [InlineData(int.MaxValue, 1, 0)]
    public void ToSnapshot_OverflowingHeaders_ReturnNull(int width, int height, int colorsUsed)
    {
        Assert.Null(DibToPng.ToSnapshot(HeaderOnly(width, height, colorsUsed), isPng: false));
    }

    // Over the pixel cap the reader skips the bitmap before copying it out
    // of the clipboard (a 12k x 12k canvas peaked near 2.5 GB).
    [Fact]
    public void CanConvert_RejectsImagesOverThePixelCap()
    {
        Assert.True(DibToPng.CanConvert(HeaderOnly(7680, 4320)));
        Assert.False(DibToPng.CanConvert(HeaderOnly(12_000, 12_000)));
        Assert.False(DibToPng.CanConvert(HeaderOnly(1, int.MinValue)));
    }

    [Fact]
    public void ToSnapshot_PngOverThePixelCap_IsSkipped()
    {
        var png = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(png, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), 20_000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), 20_000);
        png[24] = 8; png[25] = 6;

        Assert.Null(DibToPng.ToSnapshot(png, isPng: true));
    }

    [Fact]
    public void FromDib_TopDownAndBottomUp_EncodeTheSamePixels()
    {
        // 2x2, rows given top row first.
        byte[][] rows = [[1, 2, 3, 0, 4, 5, 6, 0], [7, 8, 9, 0, 10, 11, 12, 0]];
        byte[] Build(bool topDown)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(40); w.Write(2); w.Write(topDown ? -2 : 2);
            w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(16);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            foreach (var row in topDown ? rows : rows.Reverse())
            {
                w.Write(row);
            }
            return ms.ToArray();
        }

        var (_, fromTopDown) = Decode(DibToPng.FromDib(Build(topDown: true)));
        var (_, fromBottomUp) = Decode(DibToPng.FromDib(Build(topDown: false)));

        Assert.Equal(fromTopDown, fromBottomUp);
        Assert.Equal(new byte[] { 3, 2, 1, 255 }, fromTopDown[..4]); // top-left, BGR -> RGBA
    }
}
