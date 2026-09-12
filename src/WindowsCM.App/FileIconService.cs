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

public static class FileIconService
{
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000; // 32x32
    private const uint SHGFI_SMALLICON = 0x000000001; // 16x16
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetFileIcon(string? rawPath, bool isLarge = true)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var path = FileDisplayHelper.NormalizePath(rawPath);
        var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
        var isDir = Directory.Exists(path);

        // Cache key strategy: .exe, .ico, .lnk icon can differ per file; generic extensions share the same icon.
        var cacheKey = isDir
            ? (isLarge ? "__dir_large__" : "__dir_small__")
            : (ext is ".exe" or ".ico" or ".lnk"
                ? $"{path}_{(isLarge ? "L" : "S")}"
                : $"{ext}_{(isLarge ? "L" : "S")}");

        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var iconSource = ExtractIcon(path, ext, isDir, isLarge);
        if (iconSource != null)
        {
            Cache[cacheKey] = iconSource;
        }

        return iconSource;
    }

    private static ImageSource? ExtractIcon(string path, string ext, bool isDir, bool isLarge)
    {
        var flags = SHGFI_ICON | (isLarge ? SHGFI_LARGEICON : SHGFI_SMALLICON);
        SHFILEINFO shinfo = default;

        // Try extracting directly if path exists
        if (File.Exists(path) || isDir)
        {
            var res = SHGetFileInfo(path, 0, out shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                return ConvertHIconToBitmapSource(shinfo.hIcon);
            }
        }

        // Fallback: extract using attributes and extension (works even when file was deleted or disk unmounted)
        var attr = isDir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        var probeTarget = isDir ? "dummy_folder" : (string.IsNullOrEmpty(ext) ? "file.txt" : $"dummy{ext}");
        var fallbackRes = SHGetFileInfo(probeTarget, attr, out shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), flags | SHGFI_USEFILEATTRIBUTES);

        if (fallbackRes != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
        {
            return ConvertHIconToBitmapSource(shinfo.hIcon);
        }

        return null;
    }

    private static ImageSource? ConvertHIconToBitmapSource(IntPtr hIcon)
    {
        try
        {
            var bs = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze(); // Freeze for cross-thread safety & zero re-render overhead
            return bs;
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
