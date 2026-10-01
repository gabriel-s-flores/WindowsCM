// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.Diagnostics;
using WindowsCM.Core.Popup;

namespace WindowsCM.App;

// Decoded card thumbnails for image files (captured PNGs and image file
// items). The card converter used to decode the full-size file on the UI
// thread every time a card was realized, so opening or scrolling a popup
// full of screenshots stuttered. Now each file decodes once into a bounded
// LRU (keyed by path + write time), and new captures plus the images
// already in history are decoded ahead of time on a worker thread.
internal static class ImageThumbnailCache
{
    private const int DecodePixelHeight = 180;
    private const int DecodePixelWidth = 320;

    // ~230 KB per 320x180 thumbnail: each cache tier holds at most ~30 MB.
    private static readonly LruCache<string, ImageSource?> Cache = new(128, StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim DecodeSlots = new(2);

    // Path resolution, file metadata and decoding all happen on the worker.
    // The UI only reads this bounded cache, including cached missing files.
    private static readonly BackgroundProbeCache<ImageSource?> AsyncCache = new(
        key =>
        {
            var banner = key.EndsWith("|banner", StringComparison.Ordinal);
            var path = banner ? key[..^7] : key;
            var version = KeyFor(path);
            if (version is null) return null;
            if (banner) version += "|banner";
            if (Cache.TryGet(version, out var cached)) return cached;
            var decoded = TryDecode(path, banner);
            Cache.Set(version, decoded);
            return decoded;
        },
        work => _ = Task.Run(async () =>
        {
            await DecodeSlots.WaitAsync().ConfigureAwait(false);
            try { work(); }
            finally { DecodeSlots.Release(); }
        }),
        freshFor: TimeSpan.FromSeconds(30),
        capacity: 128,
        comparer: StringComparer.OrdinalIgnoreCase);

    // Raised on a worker thread when a queued decode lands (or fails):
    // marshal before touching the UI.
    public static event Action? Updated;

    static ImageThumbnailCache() => AsyncCache.Updated += _ => Updated?.Invoke();

    // UI-thread lookup for card images (captured screenshots, link preview
    // images): never decodes here. A miss — an older screenshot scrolling
    // into view, one evicted from the cache, any image after a restart —
    // used to decode the full file on the UI thread, 50–200 ms per 4K card.
    // Null until the decode lands and Updated fires; a file that cannot be
    // decoded is remembered as such.
    public static ImageSource? GetOrQueue(string path, bool banner = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var key = banner ? path + "|banner" : path;
        return AsyncCache.TryGet(key, out var image) ? image : null;
    }

    // Background STA lookup for CardFileFacts: decode on a miss, falling
    // back to the Shell thumbnail. Never call this from the UI thread.
    public static ImageSource? Get(string path)
    {
        var key = KeyFor(path);
        if (key is null)
        {
            return null;
        }
        if (Cache.TryGet(key, out var cached))
        {
            return cached;
        }
        var image = TryDecode(path) ?? ThumbnailService.GetThumbnail(path);
        Cache.Set(key, image);
        return image;
    }

    // Shares the same bounded workers/cache as cards, including negative
    // results, so prewarming and scrolling cannot duplicate every decode.
    public static void Prewarm(IEnumerable<string> paths)
    {
        var batch = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        if (batch.Count == 0)
        {
            return;
        }
        foreach (var path in batch)
        {
            GetOrQueue(path);
        }
    }

    public static void Clear()
    {
        Cache.Clear();
        AsyncCache.Clear();
    }

    private static string? KeyFor(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? $"{info.FullName}|{info.LastWriteTimeUtc.Ticks}" : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // Frozen for cross-thread use; OnLoad releases the source before return.
    // Read the dimensions first and constrain both axes without upscaling.
    // A height-only decode inflated a 1000x10 image into 18000x180 pixels;
    // a width-only banner did the same to tall images.
    private static BitmapSource? TryDecode(string path, bool banner = false)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            var scale = Math.Min(1.0, Math.Min((double)DecodePixelWidth / frame.PixelWidth, (double)DecodePixelHeight / frame.PixelHeight));
            var width = Math.Max(1, (int)(frame.PixelWidth * scale));
            var height = Math.Max(1, (int)(frame.PixelHeight * scale));
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = width;
            image.DecodePixelHeight = height;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
