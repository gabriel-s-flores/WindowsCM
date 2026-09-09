// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Text;
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Paste.Win32;

// Clipboard writer for copy-back: planned contents in, Win32 formats out.
// Text kinds become CF_UNICODETEXT (+ the opaque CF_HTML envelope verbatim
// when present); images offer CF_DIB with the PNG bytes as fallback;
// file lists become CF_HDROP with Preferred DropEffect forced to copy.
// Real-clipboard behavior is manual-smoke per Testing Decisions (no real
// clipboard in CI); the planning it consumes is fully unit-tested.
public sealed class Win32ClipboardWriter : IClipboardWriter
{
    private readonly uint _htmlFormat;
    private readonly uint _pngFormat;
    private readonly uint _dropEffectFormat;

    public Win32ClipboardWriter()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32 clipboard requires Windows.");
        }
        _htmlFormat = NativePaste.RegisterClipboardFormat("HTML Format");
        _pngFormat = NativePaste.RegisterClipboardFormat("PNG");
        _dropEffectFormat = NativePaste.RegisterClipboardFormat("Preferred DropEffect");
    }

    public void Write(ClipboardContents contents)
    {
        if (contents.Text is null && contents.ImageDib is null
            && contents.ImagePng is null && contents.FileLocalPaths is null)
        {
            throw new ClipboardWriteException("Nothing writable in the clipboard contents.");
        }
        ClipboardOwnership.AssertSta();
        var opened = ClipboardOwnership.OpenRetry(
            () => NativePaste.OpenClipboard(IntPtr.Zero));
        if (!opened)
        {
            throw new ClipboardWriteException("Could not open the clipboard (another window owns it).");
        }
        try
        {
            if (!NativePaste.EmptyClipboard())
            {
                throw new ClipboardWriteException("Could not empty the clipboard.");
            }
            if (contents.Text is not null)
            {
                SetBytes(NativePaste.CF_UNICODETEXT,
                    Encoding.Unicode.GetBytes(contents.Text + '\0'));
            }
            if (contents.Html is not null && _htmlFormat != 0)
            {
                SetBytes(_htmlFormat, Encoding.UTF8.GetBytes(contents.Html + '\0'));
            }
            if (contents.ImageDib is not null)
            {
                SetBytes(NativePaste.CF_DIB, contents.ImageDib);
            }
            if (contents.ImagePng is not null && _pngFormat != 0)
            {
                SetBytes(_pngFormat, contents.ImagePng);
            }
            if (contents.FileLocalPaths is { Count: > 0 })
            {
                SetBytes(NativePaste.CF_HDROP,
                    DropFilesBuilder.Build(contents.FileLocalPaths));
                if (_dropEffectFormat != 0)
                {
                    SetBytes(_dropEffectFormat,
                        BitConverter.GetBytes(DropFilesBuilder.CopyEffect));
                }
            }
        }
        finally
        {
            NativePaste.CloseClipboard();
        }
    }

    // Allocates a moveable handle, copies the bytes in, and hands ownership
    // to the system. On failure the handle is freed here; on success the
    // system owns it (research 02 ownership rules).
    private static void SetBytes(uint format, byte[] bytes)
    {
        var handle = NativePaste.GlobalAlloc(
            NativePaste.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
        {
            throw new ClipboardWriteException("Could not allocate clipboard memory.");
        }
        var locked = NativePaste.GlobalLock(handle);
        if (locked == IntPtr.Zero)
        {
            NativePaste.GlobalFree(handle);
            throw new ClipboardWriteException("Could not lock clipboard memory.");
        }
        try
        {
            Marshal.Copy(bytes, 0, locked, bytes.Length);
        }
        finally
        {
            NativePaste.GlobalUnlock(handle);
        }
        if (NativePaste.SetClipboardData(format, handle) == IntPtr.Zero)
        {
            NativePaste.GlobalFree(handle);
            throw new ClipboardWriteException($"Could not set clipboard format {format}.");
        }
    }
}
