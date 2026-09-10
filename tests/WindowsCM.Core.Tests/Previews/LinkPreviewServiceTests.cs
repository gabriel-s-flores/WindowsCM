// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Service seam over faked HTTP + image cache (spec Testing Decisions:
// "preview HTTP stub"). Asserts the externally visible contract: HTML-only
// metadata, URL-hash image cache, offline yields no preview, exclusions
// honored. No real sockets, no sleeps.
internal sealed class FakeLinkPreviewHttp : ILinkPreviewHttp
{
    public Dictionary<string, LinkHttpResponse> Pages = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, byte[]> Images = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Requested = [];
    public bool Offline;

    public Task<LinkHttpResponse> GetAsync(string url, CancellationToken ct = default)
    {
        Requested.Add(url);
        if (Offline)
        {
            throw new LinkPreviewUnavailableException("offline");
        }
        if (Pages.TryGetValue(url, out var page))
        {
            return Task.FromResult(page);
        }
        if (Images.TryGetValue(url, out var bytes))
        {
            return Task.FromResult(LinkHttpResponse.Image(bytes));
        }
        throw new LinkPreviewUnavailableException($"no stub for {url}");
    }

    public static LinkHttpResponse HtmlPage(string html) =>
        new("text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
}

internal sealed class FakeLinkImageCache : ILinkImageCache
{
    public Dictionary<string, byte[]> Files = new(StringComparer.Ordinal);
    public string Directory => Path.GetTempPath();

    public string? TryGet(string url) =>
        Files.ContainsKey(url) ? Path.Combine(Directory, url.GetHashCode().ToString()) : null;

    public string SaveIfAbsent(string url, byte[] bytes)
    {
        if (!Files.ContainsKey(url))
        {
            Files[url] = bytes;
        }
        return Path.Combine(Directory, url.GetHashCode().ToString());
    }
}

public sealed class LinkPreviewServiceTests
{
    private const string Page = "https://example.com/articles/1";
    private const string Html =
        "<html><head><title>T</title>" +
        "<meta property=\"og:description\" content=\"D\" />" +
        "<meta property=\"og:image\" content=\"https://cdn.example.com/i.png\" />" +
        "</head></html>";

    private readonly FakeLinkPreviewHttp _http = new();
    private readonly FakeLinkImageCache _cache = new();
    private LinkPreviewService Subject(LinkPreviewOptions? options = null) =>
        new(_http, _cache, options ?? new LinkPreviewOptions());

    [Fact]
    public async Task Fetch_HtmlPage_ReturnsMetadataAndCachesImage()
    {
        _http.Pages[Page] = FakeLinkPreviewHttp.HtmlPage(Html);
        _http.Images["https://cdn.example.com/i.png"] = [9, 9];

        var result = await Subject().FetchAsync(Page);

        Assert.NotNull(result);
        Assert.Equal("T", result.Metadata.Title);
        Assert.Equal("D", result.Metadata.Description);
        Assert.Equal("https://cdn.example.com/i.png", result.Metadata.ImageUrl);
        Assert.NotNull(result.CachedImagePath);
        Assert.True(_cache.Files.ContainsKey(Page));
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task Fetch_CacheHit_SkipsImageDownload()
    {
        _http.Pages[Page] = FakeLinkPreviewHttp.HtmlPage(Html);
        _cache.Files[Page] = [1];
        _http.Images["https://cdn.example.com/i.png"] = [9, 9];

        var result = await Subject().FetchAsync(Page);

        Assert.NotNull(result);
        Assert.NotNull(result.CachedImagePath);
        Assert.True(result.FromCache);
        Assert.DoesNotContain("https://cdn.example.com/i.png", _http.Requested);
    }

    [Fact]
    public async Task Fetch_Offline_ReturnsNull()
    {
        _http.Offline = true;

        Assert.Null(await Subject().FetchAsync(Page));
    }

    [Fact]
    public async Task Fetch_ErrorStatus_ReturnsNull()
    {
        // A 404 HTML error page is a transport failure, never a preview.
        _http.Pages[Page] = new LinkHttpResponse(
            "text/html; charset=utf-8",
            Encoding.UTF8.GetBytes("<html><head><title>Not Found</title></head></html>"),
            IsSuccess: false);

        Assert.Null(await Subject().FetchAsync(Page));
    }

    [Fact]
    public async Task Fetch_NonHtml_ReturnsNull()
    {
        _http.Pages[Page] = new LinkHttpResponse("application/json", "{}"u8.ToArray());

        Assert.Null(await Subject().FetchAsync(Page));
    }

    [Fact]
    public async Task Fetch_ImageUrlDirectly_ReturnsImagePreview()
    {
        const string direct = "https://example.com/a.png";
        _http.Pages[direct] = LinkHttpResponse.Image([7, 7, 7]);

        var result = await Subject().FetchAsync(direct);

        Assert.NotNull(result);
        Assert.Equal(direct, result.Metadata.ImageUrl);
        Assert.NotNull(result.CachedImagePath);
    }

    [Fact]
    public async Task Fetch_ExcludedUrl_ReturnsNullWithoutRequest()
    {
        var options = new LinkPreviewOptions
        {
            ExclusionPatterns = ["internal\\.example\\.com"],
        };

        var result = await Subject(options)
            .FetchAsync("https://internal.example.com/secret");

        Assert.Null(result);
        Assert.Empty(_http.Requested);
    }

    [Fact]
    public async Task Fetch_PreviewsDisabled_ReturnsNullWithoutRequest()
    {
        var options = new LinkPreviewOptions { ShowPreview = false };
        _http.Pages[Page] = FakeLinkPreviewHttp.HtmlPage(Html);

        Assert.Null(await Subject(options).FetchAsync(Page));
        Assert.Empty(_http.Requested);
    }

    [Fact]
    public async Task Fetch_ImageDownloadFails_StillReturnsTextMetadata()
    {
        _http.Pages[Page] = FakeLinkPreviewHttp.HtmlPage(Html);

        var result = await Subject().FetchAsync(Page);

        Assert.NotNull(result);
        Assert.Equal("T", result.Metadata.Title);
        Assert.Null(result.CachedImagePath);
    }

    [Fact]
    public async Task Fetch_EmptyMetadata_ReturnsNull()
    {
        _http.Pages[Page] = FakeLinkPreviewHttp.HtmlPage("<html><head></head></html>");

        Assert.Null(await Subject().FetchAsync(Page));
    }
}
