// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.Popup;

namespace WindowsCM.App;

// Extracts high-resolution visual previews and thumbnails directly from the
// Windows Shell (IShellItemImageFactory / Explorer thumbnail cache) for videos,
// photos, audio cover art, presentation slides, PDFs, etc.
public static class ThumbnailService
{
    private static readonly Guid IShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] SIIGBF flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;

        public SIZE(int cx, int cy)
        {
            this.cx = cx;
            this.cy = cy;
        }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
        SIIGBF_SCALEUP = 0x100,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetThumbnail(string? rawPath, int width = 250, int height = 160)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var path = FileDisplayHelper.NormalizePath(rawPath);
        if (!File.Exists(path))
        {
            return null;
        }

        var writeTime = File.GetLastWriteTimeUtc(path).Ticks;
        var cacheKey = $"{path}_{width}x{height}_{writeTime}";

        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var thumb = ExtractThumbnail(path, width, height);
        Cache[cacheKey] = thumb;
        return thumb;
    }

    public static Task<ImageSource?> GetThumbnailAsync(string? rawPath, int width = 250, int height = 160) =>
        Task.Run(() => GetThumbnail(rawPath, width, height));

    public static void ClearCache() => Cache.Clear();

    private static ImageSource? ExtractThumbnail(string path, int width, int height)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IShellItemImageFactory? factory = null;
        var hBitmap = IntPtr.Zero;

        try
        {
            var guid = IShellItemImageFactoryGuid;
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out factory);
            if (hr != 0 || factory == null)
            {
                return null;
            }

            var size = new SIZE(width, height);
            // SIIGBF_THUMBNAILONLY ensures we only get genuine visual previews (video frames, photo thumbnails, album art, slides)
            // and fail quickly for files that do not have visual thumbnails (falling back to the clean icon view).
            hr = factory.GetImage(size, SIIGBF.SIIGBF_THUMBNAILONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            var bs = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bs.Freeze(); // Safe for cross-thread usage and 60fps rendering in WPF
            return bs;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (factory != null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }
}
