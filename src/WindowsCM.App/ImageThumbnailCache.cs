// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.Diagnostics;

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

    // ~230 KB per 16:9 thumbnail at 180 px: ~30 MB worst case.
    private static readonly LruCache<string, ImageSource?> Cache = new(128, StringComparer.OrdinalIgnoreCase);

    // Decodes queued by GetOrQueue, at most two at a time: scrolling past a
    // hundred screenshots must not start a hundred full-size decodes.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> Queued = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim DecodeSlots = new(2);

    // Raised on a worker thread when a queued decode lands (or fails):
    // marshal before touching the UI.
    public static event Action? Updated;

    // UI-thread lookup for card images (captured screenshots, link preview
    // images): never decodes here. A miss — an older screenshot scrolling
    // into view, one evicted from the cache, any image after a restart —
    // used to decode the full file on the UI thread, 50–200 ms per 4K card.
    // Null until the decode lands and Updated fires; a file that cannot be
    // decoded is remembered as such.
    public static ImageSource? GetOrQueue(string path)
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
        if (Queued.TryAdd(key, 0))
        {
            _ = Task.Run(async () =>
            {
                await DecodeSlots.WaitAsync().ConfigureAwait(false);
                try
                {
                    Cache.Set(key, TryDecode(path));
                }
                finally
                {
                    DecodeSlots.Release();
                    Queued.TryRemove(key, out _);
                }
                Updated?.Invoke();
            });
        }
        return null;
    }

    // UI-thread lookup: decode on a miss, falling back to the Shell
    // thumbnail (STA COM, so never from the prewarm worker).
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

    // Decodes the given files on the thread pool so the popup finds them
    // ready. Failures are left uncached: the UI lookup retries them with the
    // Shell fallback.
    public static void Prewarm(IEnumerable<string> paths)
    {
        var batch = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        if (batch.Count == 0)
        {
            return;
        }
        _ = Task.Run(() =>
        {
            foreach (var path in batch)
            {
                try
                {
                    var key = KeyFor(path);
                    if (key is null || Cache.TryGet(key, out _))
                    {
                        continue;
                    }
                    if (TryDecode(path) is { } image)
                    {
                        Cache.Set(key, image);
                    }
                }
                catch
                {
                    // Best effort: the UI lookup decodes it later.
                }
            }
        });
    }

    public static void Clear() => Cache.Clear();

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

    // Frozen, so the worker-decoded bitmap can be used by the UI thread. The
    // bytes are read up front with FileShare.ReadWrite: the file is never
    // held open (or locked against the capture writer) while decoding.
    private static BitmapSource? TryDecode(string path)
    {
        try
        {
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }
            using var memory = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.DecodePixelHeight = DecodePixelHeight;
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
