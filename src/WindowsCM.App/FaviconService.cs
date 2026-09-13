// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

// Two-level cache (RAM + Disk) for website favicons with asynchronous non-blocking downloads.
// Provides frozen BitmapSource instances for 0ms rendering overhead and high UI framerate.
public static class FaviconService
{
    private static readonly ConcurrentDictionary<string, ImageSource> MemoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> ActiveDownloads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static event Action<string>? FaviconUpdated;

    private static string GetFaviconDir()
    {
        var dir = Path.Combine(AppFolders.CacheDir(), "favicons");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static ImageSource? GetFavicon(string? rawUrl)
    {
        var domain = LinkDisplayHelper.GetDomain(rawUrl);
        if (string.IsNullOrWhiteSpace(domain) || domain == "Link" || !domain.Contains('.'))
        {
            return null;
        }

        // 1. Check in-memory cache
        if (MemoryCache.TryGetValue(domain, out var cached))
        {
            return cached;
        }

        // 2. Check local disk cache
        var localPath = Path.Combine(GetFaviconDir(), $"{SanitizeFileName(domain)}.png");
        if (File.Exists(localPath))
        {
            var loaded = LoadFrozenBitmap(localPath);
            if (loaded != null)
            {
                MemoryCache[domain] = loaded;
                return loaded;
            }
        }

        // 3. Trigger asynchronous background download if not already in flight
        if (ActiveDownloads.TryAdd(domain, 0))
        {
            _ = Task.Run(() => DownloadFaviconAsync(domain, localPath));
        }

        return null;
    }

    private static async Task DownloadFaviconAsync(string domain, string localPath)
    {
        try
        {
            var cdnUrl = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=64";
            using var response = await HttpClient.GetAsync(cdnUrl).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length > 100) // Valid image payload
                {
                    var tempPath = localPath + ".tmp" + Guid.NewGuid().ToString("N")[..6];
                    await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
                    File.Move(tempPath, localPath, overwrite: true);

                    // Load frozen image on dispatcher or thread
                    var bitmap = LoadFrozenBitmap(localPath);
                    if (bitmap != null)
                    {
                        MemoryCache[domain] = bitmap;
                        // Fire event on dispatcher if active
                        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
                        {
                            _ = dispatcher.BeginInvoke(new Action(() => FaviconUpdated?.Invoke(domain)));
                        }
                        else
                        {
                            FaviconUpdated?.Invoke(domain);
                        }
                    }
                }
            }
        }
        catch
        {
            // Best effort: network failures or DNS timeouts silently degrade to fallback UI
        }
        finally
        {
            ActiveDownloads.TryRemove(domain, out _);
        }
    }

    private static ImageSource? LoadFrozenBitmap(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = 64;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
