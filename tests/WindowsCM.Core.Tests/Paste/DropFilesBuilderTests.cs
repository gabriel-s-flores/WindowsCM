// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Text;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

public sealed class DropFilesBuilderTests
{
    [Fact]
    public void Build_EmitsDropFilesWithWidePaths()
    {
        var payload = DropFilesBuilder.Build([@"C:\a.txt", @"C:\b.txt"]);

        // DROPFILES header: pFiles=20, pt=(0,0), fNC=0, fWide=TRUE.
        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(16, 4)));
        var text = Encoding.Unicode.GetString(payload[20..]);
        Assert.Equal("C:\\a.txt\0C:\\b.txt\0\0", text);
    }

    [Fact]
    public void Effect_IsAlwaysCopy()
    {
        // Ticket: file lists are written back forcing copy, even when the
        // stored operation was cut — the effect DWORD carries DROPEFFECT_COPY.
        Assert.Equal(1, DropFilesBuilder.CopyEffect);
    }

    [Fact]
    public void Build_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => DropFilesBuilder.Build([]));
    }
}
