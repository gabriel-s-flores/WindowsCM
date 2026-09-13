// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using WindowsCM.Core.Popup;

namespace WindowsCM.App;

// Extracts high-fidelity metadata (song title, artist, album, duration, video specs)
// from audio and video media files directly via the Windows Property System (IPropertyStore)
// and Explorer metadata handlers.
public static class MediaMetadataService
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, [Out] out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, [In] ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr val1;
        public IntPtr val2;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        int flags,
        ref Guid riid,
        out IPropertyStore ppv);

    [DllImport("propsys.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int PropVariantToStringAlloc(ref PROPVARIANT propvar, out IntPtr ppsz);

    [DllImport("propsys.dll", SetLastError = true)]
    private static extern int PropVariantToUInt64(ref PROPVARIANT propvar, out ulong pull);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    private static readonly Guid IPropertyStoreGuid = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

    // Standard Canonical Property Keys
    private static readonly PROPERTYKEY PKeyTitle = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
    private static readonly PROPERTYKEY PKeyDisplayArtist = new(new Guid("6B79D10C-DBA2-4CA2-909C-37EA6A6A1920"), 100);
    private static readonly PROPERTYKEY PKeyArtist = new(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), 100);
    private static readonly PROPERTYKEY PKeyAlbum = new(new Guid("56A3372E-CE9C-11D2-9F0E-006097C686F6"), 4);
    private static readonly PROPERTYKEY PKeyDuration = new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 3);

    private static readonly ConcurrentDictionary<string, AudioMetadataInfo?> AudioCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string?> MediaBadgeCache = new(StringComparer.OrdinalIgnoreCase);

    public static AudioMetadataInfo? GetAudioMetadata(string? rawPath)
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

        long lastWrite = 0;
        long fileSize = 0;
        try
        {
            var fi = new FileInfo(path);
            lastWrite = fi.LastWriteTimeUtc.Ticks;
            fileSize = fi.Length;
        }
        catch
        {
            return null;
        }

        var cacheKey = $"{path}_{lastWrite}_{(WindowsCM.Core.Localization.LocalizationManager.IsPortuguese ? "pt" : "en")}";
        if (AudioCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var result = ExtractAudioMetadata(path, fileSize);
        AudioCache[cacheKey] = result;
        return result;
    }

    public static string? GetMediaOverlayBadge(string? rawPath)
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

        long lastWrite = 0;
        long fileSize = 0;
        try
        {
            var fi = new FileInfo(path);
            lastWrite = fi.LastWriteTimeUtc.Ticks;
            fileSize = fi.Length;
        }
        catch
        {
            return null;
        }

        var cacheKey = $"{path}_{lastWrite}_{(WindowsCM.Core.Localization.LocalizationManager.IsPortuguese ? "pt" : "en")}";
        if (MediaBadgeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var duration = ExtractMediaDuration(path);
        var formattedDuration = FileDisplayHelper.FormatDuration(duration);
        var formattedSize = FileDisplayHelper.FormatFileSize(fileSize);

        string badge;
        if (!string.IsNullOrWhiteSpace(formattedDuration) && !string.IsNullOrWhiteSpace(formattedSize))
        {
            badge = $"{formattedDuration} • {formattedSize}";
        }
        else if (!string.IsNullOrWhiteSpace(formattedDuration))
        {
            badge = formattedDuration;
        }
        else
        {
            badge = formattedSize;
        }

        MediaBadgeCache[cacheKey] = badge;
        return badge;
    }

    public static void ClearCache()
    {
        AudioCache.Clear();
        MediaBadgeCache.Clear();
    }

    private static AudioMetadataInfo ExtractAudioMetadata(string path, long fileSizeBytes)
    {
        var cleanFileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(cleanFileName))
        {
            cleanFileName = Path.GetFileName(path);
        }

        var formattedSize = FileDisplayHelper.FormatFileSize(fileSizeBytes);

        if (!OperatingSystem.IsWindows())
        {
            return new AudioMetadataInfo(cleanFileName, "", "", null, "", fileSizeBytes, formattedSize);
        }

        IPropertyStore? store = null;
        try
        {
            var riid = IPropertyStoreGuid;
            var hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref riid, out store);
            if (hr != 0 || store == null)
            {
                return new AudioMetadataInfo(cleanFileName, "", "", null, "", fileSizeBytes, formattedSize);
            }

            var title = ReadString(store, PKeyTitle);
            var artist = ReadString(store, PKeyDisplayArtist) ?? ReadString(store, PKeyArtist);
            var album = ReadString(store, PKeyAlbum);

            TimeSpan? duration = null;
            if (ReadUInt64(store, PKeyDuration, out var duration100ns) && duration100ns > 0)
            {
                duration = TimeSpan.FromTicks((long)duration100ns);
            }

            var formattedDuration = FileDisplayHelper.FormatDuration(duration);

            var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title.Trim() : cleanFileName;
            var resolvedArtist = artist?.Trim() ?? "";
            var resolvedAlbum = album?.Trim() ?? "";

            return new AudioMetadataInfo(
                Title: resolvedTitle,
                Artist: resolvedArtist,
                Album: resolvedAlbum,
                Duration: duration,
                FormattedDuration: formattedDuration,
                FileSizeBytes: fileSizeBytes,
                FormattedSize: formattedSize);
        }
        catch
        {
            return new AudioMetadataInfo(cleanFileName, "", "", null, "", fileSizeBytes, formattedSize);
        }
        finally
        {
            if (store != null)
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    private static TimeSpan? ExtractMediaDuration(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IPropertyStore? store = null;
        try
        {
            var riid = IPropertyStoreGuid;
            var hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref riid, out store);
            if (hr != 0 || store == null)
            {
                return null;
            }

            if (ReadUInt64(store, PKeyDuration, out var duration100ns) && duration100ns > 0)
            {
                return TimeSpan.FromTicks((long)duration100ns);
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (store != null)
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    private static string? ReadString(IPropertyStore store, PROPERTYKEY key)
    {
        PROPVARIANT pv;
        var hr = store.GetValue(ref key, out pv);
        if (hr != 0 || pv.vt == 0)
        {
            return null;
        }

        try
        {
            var allocHr = PropVariantToStringAlloc(ref pv, out var ptr);
            if (allocHr == 0 && ptr != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStringUni(ptr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
            return null;
        }
        finally
        {
            PropVariantClear(ref pv);
        }
    }

    private static bool ReadUInt64(IPropertyStore store, PROPERTYKEY key, out ulong value)
    {
        PROPVARIANT pv;
        var hr = store.GetValue(ref key, out pv);
        if (hr != 0 || pv.vt == 0)
        {
            value = 0;
            return false;
        }

        try
        {
            return PropVariantToUInt64(ref pv, out value) == 0;
        }
        finally
        {
            PropVariantClear(ref pv);
        }
    }
}
