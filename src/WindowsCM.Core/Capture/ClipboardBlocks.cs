// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Capture;

// Decoding of clipboard memory blocks already copied out of the clipboard
// (bounded by GlobalSize). The contents are the source app's word, so
// nothing here reads past the block.
public static class ClipboardBlocks
{
    // DROPFILES: pFiles (DWORD), pt (POINT), fNC (BOOL), fWide (BOOL).
    private const int DropFilesHeaderSize = 20;

    // CF_UNICODETEXT: UTF-16 up to the first NUL, or the whole block when
    // the source app left the terminator out. Reading until a NUL walked
    // past the block: garbage appended to the copy, or an access violation
    // that .NET cannot catch.
    public static string DecodeUnicodeText(ReadOnlySpan<byte> block)
    {
        var chars = MemoryMarshal.Cast<byte, char>(block[..(block.Length & ~1)]);
        var end = chars.IndexOf('\0');
        return new string(end >= 0 ? chars[..end] : chars);
    }

    // CF_HDROP: a DROPFILES header, then NUL-separated paths ended by an
    // empty one. One pass: DragQueryFile(i) walks the list from the start
    // for every i, and it ran twice per file with the clipboard open — for
    // a 50,000-file Explorer copy, tens of seconds in which no app could
    // copy or paste. Null for an ANSI list (fWide = 0), left to the system.
    public static IReadOnlyList<string>? ParseDropFiles(ReadOnlySpan<byte> block)
    {
        if (block.Length < DropFilesHeaderSize)
        {
            return [];
        }
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(block);
        if (BinaryPrimitives.ReadInt32LittleEndian(block[16..]) == 0)
        {
            return null;
        }
        if (offset < DropFilesHeaderSize || offset >= (uint)block.Length)
        {
            return [];
        }
        var list = block[(int)offset..];
        var chars = MemoryMarshal.Cast<byte, char>(list[..(list.Length & ~1)]);
        var paths = new List<string>();
        while (!chars.IsEmpty)
        {
            var end = chars.IndexOf('\0');
            var path = end >= 0 ? chars[..end] : chars;
            if (path.IsEmpty)
            {
                break;
            }
            paths.Add(new string(path));
            if (end < 0)
            {
                break;
            }
            chars = chars[(end + 1)..];
        }
        return paths;
    }
}
