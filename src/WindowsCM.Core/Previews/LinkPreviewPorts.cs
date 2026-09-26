// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Previews;

// Transport failure: offline, DNS, TLS, timeout, or a stub with no canned
// page. The service maps it to "no preview" (Copyous best-effort parity:
// no retry, no toast). Never thrown for caller cancellation — that also
// maps to no preview via OperationCanceledException.
public sealed class LinkPreviewUnavailableException : Exception
{
    public LinkPreviewUnavailableException(string message)
        : base(message)
    {
    }

    public LinkPreviewUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

// One fetched URL: content-type gated by the service (HTML-only metadata,
// image/* direct). Body is raw bytes; HTML is UTF-8 decoded by the service
// (charset sniffing deferred to post-v1). IsSuccess mirrors the HTTP
// status: error pages (404/500 HTML) are transport failures, never
// previews — the service maps them to "no preview" like offline.
public sealed record LinkHttpResponse(string? ContentType, byte[] Body, bool IsSuccess = true)
{
    public static LinkHttpResponse Html(string html) =>
        new("text/html; charset=utf-8", System.Text.Encoding.UTF8.GetBytes(html));

    public static LinkHttpResponse Image(byte[] bytes) =>
        new("image/png", bytes);

    public bool IsHtml =>
        ContentType is not null
        && (ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            || ContentType.StartsWith("text/html;", StringComparison.OrdinalIgnoreCase));

    // oEmbed answers (YouTube titles).
    public bool IsJson =>
        ContentType is not null
        && (ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || ContentType.StartsWith("text/json", StringComparison.OrdinalIgnoreCase));

    public bool IsImage =>
        ContentType is not null
        && ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

// HTTP boundary behind link previews. Tests fake it with canned pages;
// production reuses one static HttpClient (research 05 §6: 5s timeout,
// product User-Agent, header-first reads, cancelled on selection change).
public interface ILinkPreviewHttp
{
    Task<LinkHttpResponse> GetAsync(string url, CancellationToken ct = default);
}

// Production transport (research 05 §6): singleton HttpClient, 5s timeout
// (Copyous Soup idle_timeout:5 parity; .NET default 100s is too long for
// hover/cards), Accept: text/html, ResponseHeadersRead so cancellation
// stops a slow body, transport failures wrapped as unavailable.
//
// Every copied link is fetched in the background, so a link to a large file
// or a stream must cost next to nothing: only what a preview can use is read
// (a page, an image, oEmbed JSON), only so much of it, and within a body
// budget — HttpClient.Timeout stops at the headers here, and a link to an
// ISO, a zip or a live stream used to be downloaded whole into memory.
public sealed class LinkPreviewHttpClient : ILinkPreviewHttp
{
    public const int TimeoutSeconds = 5;
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

    // The tags a preview needs are in the <head>: a longer page is cut.
    public const int MaxHtmlBytes = 1024 * 1024;

    // An image or oEmbed answer over this is not used at all.
    public const int MaxBodyBytes = 5 * 1024 * 1024;

    public static readonly TimeSpan DefaultBodyTimeout = TimeSpan.FromSeconds(10);

    private static readonly HttpClient Shared = CreateShared();

    private readonly HttpClient _client;
    private readonly TimeSpan _bodyTimeout;

    public LinkPreviewHttpClient()
        : this(Shared, null)
    {
    }

    internal LinkPreviewHttpClient(HttpClient client, TimeSpan? bodyTimeout)
    {
        _client = client;
        _bodyTimeout = bodyTimeout ?? DefaultBodyTimeout;
    }

    private static HttpClient CreateShared()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
        return client;
    }

    public async Task<LinkHttpResponse> GetAsync(string url, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or InvalidOperationException or NotSupportedException or ArgumentException or UriFormatException)
        {
            // Also an og:image that is not http(s) (data:, relative, ftp:).
            throw new LinkPreviewUnavailableException($"Preview fetch failed for {url}.", ex);
        }
        using (response)
        {
            var head = new LinkHttpResponse(
                response.Content.Headers.ContentType?.ToString(), [], response.IsSuccessStatusCode);
            if (!head.IsSuccess || !(head.IsHtml || head.IsImage || head.IsJson))
            {
                return head;
            }
            var limit = head.IsHtml ? MaxHtmlBytes : MaxBodyBytes;
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budget.CancelAfter(_bodyTimeout);
                var (body, truncated) = await ReadBoundedAsync(response.Content, limit, budget.Token).ConfigureAwait(false);
                if (truncated && !head.IsHtml)
                {
                    // Part of an image or of JSON is useless.
                    throw new LinkPreviewUnavailableException($"Preview body over {limit} bytes for {url}.");
                }
                return head with { Body = body };
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException)
            {
                throw new LinkPreviewUnavailableException($"Preview read failed for {url}.", ex);
            }
        }
    }

    private static async Task<(byte[] Body, bool Truncated)> ReadBoundedAsync(
        HttpContent content, int limit, CancellationToken ct)
    {
        using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var body = new MemoryStream();
        var chunk = new byte[64 * 1024];
        while (body.Length < limit)
        {
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - body.Length)), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return (body.ToArray(), false);
            }
            body.Write(chunk, 0, read);
        }
        var more = await stream.ReadAsync(chunk.AsMemory(0, 1), ct).ConfigureAwait(false);
        return (body.ToArray(), more > 0);
    }
}
