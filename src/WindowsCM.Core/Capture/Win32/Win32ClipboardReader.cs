// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Text;
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Capture.Win32;

// Win32 clipboard reader: listener events in, copied-out payloads out.
// Ownership: STA-gated, OpenClipboard retried with backoff, every handle
// copied immediately (never freed/locked), CloseClipboard in finally.
// Probe order Image > File > Text (Copyous parity); CF_HTML read opaque.
public sealed class Win32ClipboardReader : IClipboardReader
{
    private readonly uint _pngFormat;
    private readonly uint _htmlFormat;
    private readonly uint _dropEffectFormat;

    public Win32ClipboardReader()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32 clipboard requires Windows.");
        }
        _pngFormat = NativeClipboard.RegisterClipboardFormat("PNG");
        _htmlFormat = NativeClipboard.RegisterClipboardFormat("HTML Format");
        _dropEffectFormat = NativeClipboard.RegisterClipboardFormat("Preferred DropEffect");
    }

    public ClipboardPayload? Read()
    {
        ClipboardOwnership.AssertSta();
        var opened = ClipboardOwnership.OpenRetry(
            () => NativeClipboard.OpenClipboard(IntPtr.Zero));
        if (!opened)
        {
            return null;
        }
        List<string> formats;
        (byte[] Bytes, bool IsPng)? rawImage;
        FileSnapshot? files;
        string? text;
        string? html;
        try
        {
            // Only copy bytes out while the clipboard is open: until
            // CloseClipboard every other app's copy and paste fails, so the
            // PNG encoding of a screenshot happens after it (below).
            formats = GetFormatNames();
            rawImage = TryReadRawImage();
            files = TryReadFiles();
            text = TryReadText();
            html = TryReadHtml();
        }
        finally
        {
            NativeClipboard.CloseClipboard();
        }
        var image = rawImage is { } raw ? DibToPng.ToSnapshot(raw.Bytes, raw.IsPng) : null;
        if (image is null && files is null && string.IsNullOrWhiteSpace(text) && html is null)
        {
            return string.IsNullOrWhiteSpace(text) ? null
                : new ClipboardPayload(null, null, text, formats, html);
        }
        return new ClipboardPayload(image, files, text, formats, html);
    }

    private (byte[] Bytes, bool IsPng)? TryReadRawImage()
    {
        // PNG first (exact bytes, no conversion), then the first DIBV5/DIB
        // whose header the encoder accepts (checked on 40 bytes, so only
        // one full bitmap is copied). CF_BITMAP skipped deliberately:
        // device-dependent per research 02.
        if (_pngFormat != 0 && NativeClipboard.IsClipboardFormatAvailable(_pngFormat))
        {
            var bytes = ReadBytes(_pngFormat);
            if (bytes is { Length: > 0 })
            {
                return (bytes, true);
            }
        }
        foreach (var format in new[] { NativeClipboard.CF_DIBV5, NativeClipboard.CF_DIB })
        {
            if (!NativeClipboard.IsClipboardFormatAvailable(format)
                || !DibToPng.CanConvert(PeekBytes(format, 40)))
            {
                continue;
            }
            var dib = ReadBytes(format);
            if (dib is { Length: > 40 })
            {
                return (dib, false);
            }
        }
        return null;
    }

    // The first bytes of a clipboard format (a bitmap header) without
    // copying the whole block.
    private static byte[] PeekBytes(uint format, int count)
    {
        var handle = NativeClipboard.GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return [];
        }
        var ptr = NativeClipboard.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return [];
        }
        try
        {
            var size = (int)Math.Min(count, (long)NativeClipboard.GlobalSize(handle));
            if (size <= 0)
            {
                return [];
            }
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            NativeClipboard.GlobalUnlock(handle);
        }
    }

    private FileSnapshot? TryReadFiles()
    {
        var hDrop = NativeClipboard.GetClipboardData(NativeClipboard.CF_HDROP);
        if (hDrop == IntPtr.Zero)
        {
            return null;
        }
        // The HDROP handle belongs to the clipboard: DragQueryFile copies the
        // paths out; the handle is never freed here.
        var count = NativeClipboard.DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
        if (count == 0)
        {
            return null;
        }
        var paths = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var len = NativeClipboard.DragQueryFileW(hDrop, i, null, 0);
            var sb = new StringBuilder((int)len + 1);
            NativeClipboard.DragQueryFileW(hDrop, i, sb, (uint)sb.Capacity);
            paths.Add(new Uri(sb.ToString()).AbsoluteUri);
        }
        return new FileSnapshot(paths, ReadDropEffect());
    }

    private FileOperation ReadDropEffect()
    {
        if (_dropEffectFormat == 0)
        {
            return FileOperation.Copy;
        }
        var handle = NativeClipboard.GetClipboardData(_dropEffectFormat);
        if (handle == IntPtr.Zero)
        {
            return FileOperation.Copy;
        }
        var ptr = NativeClipboard.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return FileOperation.Copy;
        }
        try
        {
            return FileDrop.FromDropEffect(Marshal.ReadInt32(ptr));
        }
        finally
        {
            NativeClipboard.GlobalUnlock(handle);
        }
    }

    private static string? TryReadText()
    {
        if (!NativeClipboard.IsClipboardFormatAvailable(NativeClipboard.CF_UNICODETEXT))
        {
            return null;
        }
        var handle = NativeClipboard.GetClipboardData(NativeClipboard.CF_UNICODETEXT);
        if (handle == IntPtr.Zero)
        {
            return null;
        }
        // HGLOBAL text: lock, copy the string out, unlock — never leave locked.
        var ptr = NativeClipboard.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            NativeClipboard.GlobalUnlock(handle);
        }
    }

    private string? TryReadHtml()
    {
        if (_htmlFormat == 0 || !NativeClipboard.IsClipboardFormatAvailable(_htmlFormat))
        {
            return null;
        }
        var bytes = ReadBytes(_htmlFormat);
        if (bytes is null)
        {
            return null;
        }
        // The GMEM block is NUL-terminated (C-string artifact, not format
        // data): trim trailing zeros so the opaque payload ends at EndHTML.
        var length = bytes.Length;
        while (length > 0 && bytes[length - 1] == 0)
        {
            length--;
        }
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private static byte[]? ReadBytes(uint format)
    {
        var handle = NativeClipboard.GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return null;
        }
        var ptr = NativeClipboard.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var size = (int)NativeClipboard.GlobalSize(handle);
            if (size <= 0)
            {
                return null;
            }
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            NativeClipboard.GlobalUnlock(handle);
        }
    }

    private static List<string> GetFormatNames()
    {
        var names = new List<string>();
        var format = NativeClipboard.EnumClipboardFormats(0);
        while (format != 0)
        {
            var sb = new StringBuilder(256);
            if (NativeClipboard.GetClipboardFormatName(format, sb, sb.Capacity) != 0)
            {
                names.Add(sb.ToString());
            }
            format = NativeClipboard.EnumClipboardFormats(format);
        }
        return names;
    }
}
