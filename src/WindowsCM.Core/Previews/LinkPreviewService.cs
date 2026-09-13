// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics.CodeAnalysis;
using System.Text;

using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Previews;

// Link preview tunables (Copyous per-type link prefs, 01 §5): preview and
// preview-image default true, exclusion regexes default empty.
public sealed class LinkPreviewOptions
{
    public bool ShowPreview { get; set; } = true;
    public bool ShowImage { get; set; } = true;
    public List<string> ExclusionPatterns { get; set; } = [];
}

// A fetched preview: parsed metadata plus the cached thumbnail path (null
// when the page has no image, images are disabled, or the image download
// failed best-effort). FromCache tells the card it skipped the download.
public sealed record LinkPreviewResult(
    LinkMetadata Metadata,
    string? CachedImagePath,
    bool FromCache);

// Orchestrates exclusions -> page fetch -> HTML-only parse -> URL-hash
// image cache (research 05 §6, 01 §8). Offline or non-HTML yields no
// preview (null): no retry, no toast. A failed og:image download still
// returns the text metadata without a thumbnail.
public sealed partial class LinkPreviewService

{
    private readonly ILinkPreviewHttp _http;
    private readonly ILinkImageCache _cache;
    private readonly LinkPreviewOptions _options;

    public LinkPreviewService(
        ILinkPreviewHttp http,
        ILinkImageCache cache,
        LinkPreviewOptions options)
    {
        _http = http;
        _cache = cache;
        _options = options;
    }

    public async Task<LinkPreviewResult?> FetchAsync(string url, CancellationToken ct = default)
    {
        if (!_options.ShowPreview
            || string.IsNullOrWhiteSpace(url)
            || LinkExclusions.IsExcluded(url, _options.ExclusionPatterns)
            || !IsHttpUrl(url))
        {
            return null;
        }

        if (TryExtractYouTubeVideoId(url, out var videoId))
        {
            return await FetchYouTubePreviewAsync(url, videoId, ct).ConfigureAwait(false);
        }

        LinkHttpResponse page;
        try
        {
            page = await _http.GetAsync(url, ct).ConfigureAwait(false);
        }

        catch (LinkPreviewUnavailableException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        // image/* direct becomes {image:url} with cache (01 §8 parity).
        if (!page.IsSuccess)
        {
            return null;
        }
        if (page.IsImage)
        {
            return ImageDirect(url, page.Body);
        }
        if (!page.IsHtml)
        {
            return null;
        }

        var metadata = LinkMetadataParser.Parse(Encoding.UTF8.GetString(page.Body), url);
        if (metadata.IsEmpty)
        {
            return null;
        }
        if (metadata.ImageUrl is null || !_options.ShowImage)
        {
            return new LinkPreviewResult(metadata, null, false);
        }

        var cached = _cache.TryGet(url);
        if (cached is not null)
        {
            return new LinkPreviewResult(metadata, cached, true);
        }
        try
        {
            var image = await _http.GetAsync(metadata.ImageUrl, ct).ConfigureAwait(false);
            if (!image.IsImage)
            {
                return new LinkPreviewResult(metadata, null, false);
            }
            return new LinkPreviewResult(
                metadata, _cache.SaveIfAbsent(url, image.Body), false);
        }
        catch (LinkPreviewUnavailableException)
        {
            return new LinkPreviewResult(metadata, null, false);
        }
        catch (OperationCanceledException)
        {
            return new LinkPreviewResult(metadata, null, false);
        }
    }

    private LinkPreviewResult? ImageDirect(string url, byte[] bytes)
    {
        var metadata = new LinkMetadata(null, null, url);
        if (!_options.ShowImage)
        {
            return new LinkPreviewResult(metadata, null, false);
        }
        var cached = _cache.TryGet(url);
        if (cached is not null)
        {
            return new LinkPreviewResult(metadata, cached, true);
        }
        return new LinkPreviewResult(metadata, _cache.SaveIfAbsent(url, bytes), false);
    }

    public static bool TryExtractYouTubeVideoId(string url, [NotNullWhen(true)] out string? videoId)
    {
        videoId = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = YouTubeRegex().Match(url);
        if (match.Success && match.Groups["id"].Success)
        {
            videoId = match.Groups["id"].Value;
            return true;
        }

        return false;
    }

    private async Task<LinkPreviewResult?> FetchYouTubePreviewAsync(string url, string videoId, CancellationToken ct)
    {
        var thumbUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
        string? title = null;
        string? author = "YouTube";

        // Attempt oEmbed resolution for video title and author name
        try
        {
            var oembedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
            var oembedResp = await _http.GetAsync(oembedUrl, ct).ConfigureAwait(false);
            if (oembedResp.IsSuccess && oembedResp.Body.Length > 0)
            {
                using var doc = JsonDocument.Parse(oembedResp.Body);
                if (doc.RootElement.TryGetProperty("title", out var tProp) && tProp.ValueKind == JsonValueKind.String)
                {
                    title = tProp.GetString();
                }
                if (doc.RootElement.TryGetProperty("author_name", out var aProp) && aProp.ValueKind == JsonValueKind.String)
                {
                    author = aProp.GetString();
                }
                if (doc.RootElement.TryGetProperty("thumbnail_url", out var thProp) && thProp.ValueKind == JsonValueKind.String)
                {
                    var customThumb = thProp.GetString();
                    if (!string.IsNullOrWhiteSpace(customThumb))
                    {
                        thumbUrl = customThumb;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is LinkPreviewUnavailableException or OperationCanceledException or JsonException)
        {
            // oEmbed failed; degrade gracefully to standard title fallback
        }

        title ??= "Vídeo do YouTube";
        var metadata = new LinkMetadata(title, author, thumbUrl);

        if (!_options.ShowImage)
        {
            return new LinkPreviewResult(metadata, null, false);
        }

        var cached = _cache.TryGet(url);
        if (cached is not null)
        {
            return new LinkPreviewResult(metadata, cached, true);
        }

        try
        {
            var imgResp = await _http.GetAsync(thumbUrl, ct).ConfigureAwait(false);
            if (imgResp.IsSuccess && imgResp.Body.Length > 0)
            {
                var cachedPath = _cache.SaveIfAbsent(url, imgResp.Body);
                return new LinkPreviewResult(metadata, cachedPath, false);
            }
            return new LinkPreviewResult(metadata, null, false);
        }
        catch (Exception ex) when (ex is LinkPreviewUnavailableException or OperationCanceledException)
        {
            return new LinkPreviewResult(metadata, null, false);
        }
    }

    [GeneratedRegex(@"(?:https?:\/\/)?(?:www\.|m\.)?(?:youtube\.com\/(?:watch\?(?:.*&)?v=|shorts\/|embed\/)|youtu\.be\/)(?<id>[\w-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex();

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

