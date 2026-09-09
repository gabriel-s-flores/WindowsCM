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
}
