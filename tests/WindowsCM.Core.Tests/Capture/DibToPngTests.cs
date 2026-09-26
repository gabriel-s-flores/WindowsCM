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
        var bitfields = DIB1x1(1, 2, 3);
        bitfields[16] = 3; // BI_BITFIELDS, typical of CF_DIBV5 from 32bpp sources

        Assert.True(DibToPng.CanConvert(rgb32.AsSpan(0, 40)));
        Assert.True(DibToPng.CanConvert(rgb24.AsSpan(0, 40)));
        Assert.False(DibToPng.CanConvert(paletted.AsSpan(0, 40)));
        Assert.False(DibToPng.CanConvert(bitfields.AsSpan(0, 40)));
        Assert.False(DibToPng.CanConvert(rgb32.AsSpan(0, 20)));
        Assert.Throws<NotSupportedException>(() => DibToPng.FromDib(bitfields));
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
}
