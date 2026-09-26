// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

namespace WindowsCM.App;

// What a File / Files card shows about its (first) path. Everything here
// comes from the disk or the Shell, so it is gathered off the UI thread.
internal sealed record FileFacts(
    bool Exists,
    bool IsDirectory,
    long? Size,
    ImageSource? Thumbnail,
    ImageSource? Icon,
    ImageSource? AudioCover,
    AudioMetadataInfo? Audio,
    string? VideoBadge)
{
    public static readonly FileFacts Missing = new(false, false, null, null, null, null, null, null);
}

// Card converters read file facts only from here. A card used to call
// File.Exists, FileInfo, the Shell thumbnail/icon extractors and the
// property store directly — up to ~20 calls per File card, every time it
// scrolled into view, all on the UI thread. On a path that answers slowly
// (a share that is offline, WSL, a sleeping drive, a removed USB stick)
// each call blocked for seconds and scrolling froze the popup. A lookup now
// never touches the disk: a miss queues the probe on a background STA
// thread (the Shell handlers want one) and Updated fires when facts land.
internal static class CardFileFacts
{
    private static readonly StaWorker Worker = new("WindowsCM card file probe");

    private static readonly BackgroundProbeCache<FileFacts> Cache = new(
        Probe,
        Worker.Post,
        freshFor: TimeSpan.FromSeconds(30),
        capacity: 256,
        comparer: StringComparer.OrdinalIgnoreCase,
        onProbeFailed: (_, ex) => (System.Windows.Application.Current as App)?.LogError("card-probe", ex));

    // Raised on the probe thread: marshal before touching the UI.
    public static event Action? Updated;

    static CardFileFacts() => Cache.Updated += _ => Updated?.Invoke();

    // Null while the path has not been probed yet (the card shows its
    // extension icon and name meanwhile).
    public static FileFacts? TryGet(string? rawPath)
    {
        var path = FileDisplayHelper.NormalizePath(rawPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        return Cache.TryGet(path, out var facts) ? facts : null;
    }

    // Facts for the first path of a File / Files item; null for every
    // other kind and while the path is being probed.
    public static FileFacts? ForItem(ClipboardItem item) =>
        item.Kind is ItemKind.File or ItemKind.Files
            ? TryGet(TextPreview.FirstNonEmptySegment(item.Content))
            : null;

    // Formatted answers (sizes, durations) follow the UI language.
    public static void Clear() => Cache.Clear();

    // Name, type and — for a single file — the probed size.
    public static FileDisplayDetails? DetailsFor(ClipboardItem item) =>
        FileDisplayHelper.GetFileDetails(item, probeFileSize: path => TryGet(path)?.Size);

    private static FileFacts Probe(string path)
    {
        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            return FileFacts.Missing;
        }
        long? size = null;
        ImageSource? thumbnail = null;
        ImageSource? cover = null;
        AudioMetadataInfo? audio = null;
        string? videoBadge = null;
        if (!isDirectory)
        {
            size = new FileInfo(path).Length;
            thumbnail = ItemDisplayFormatter.IsImageFilePath(path)
                ? ImageThumbnailCache.Get(path)
                : ThumbnailService.GetThumbnail(path);
            if (MediaItemClassifier.IsAudioPath(path))
            {
                audio = MediaMetadataService.GetAudioMetadata(path);
                cover = ThumbnailService.GetThumbnail(path, 160, 160);
            }
            else if (MediaItemClassifier.IsVideoPath(path))
            {
                videoBadge = MediaMetadataService.GetMediaOverlayBadge(path);
            }
        }
        var icon = FileIconService.GetFileIcon(path, isLarge: true);
        return new FileFacts(true, isDirectory, size, thumbnail, icon, cover, audio, videoBadge);
    }
}

// One background STA thread running queued work in order. The Shell
// thumbnail, icon and property handlers expect an STA; from an MTA thread
// COM would marshal apartment-threaded handlers back to the main STA — the
// UI thread this exists to keep free.
internal sealed class StaWorker
{
    private readonly BlockingCollection<Action> _queue = new();

    public StaWorker(string name)
    {
        var thread = new Thread(Run) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Post(Action work) => _queue.Add(work);

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch
            {
                // Work items report their own failures; the thread must live on.
            }
        }
    }
}
