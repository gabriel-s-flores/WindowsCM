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
public sealed class LinkPreviewHttpClient : ILinkPreviewHttp
{
    public const int TimeoutSeconds = 5;
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

    private static readonly HttpClient Shared = CreateShared();

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
            response = await Shared.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LinkPreviewUnavailableException($"Preview fetch failed for {url}.", ex);
        }
        using (response)
        {
            byte[] body;
            try
            {
                body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new LinkPreviewUnavailableException($"Preview read cancelled for {url}.", ex);
            }
            var contentType = response.Content.Headers.ContentType?.ToString();
            return new LinkHttpResponse(contentType, body, response.IsSuccessStatusCode);
        }
    }
}
