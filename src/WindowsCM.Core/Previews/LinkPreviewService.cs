// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

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
public sealed class LinkPreviewService
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

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
