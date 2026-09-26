// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// The production transport over a stub handler: what it reads of a
// response, and when it gives up. Every copied link is fetched in the
// background, so a link to a large file or a stream must cost almost
// nothing.
public sealed class LinkPreviewHttpClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    // Counts what the client actually pulled from the body.
    private sealed class CountingStream(long length, byte fill = (byte)'a') : Stream
    {
        public long Served;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Served; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - Served);
            Array.Fill(buffer, fill, offset, n);
            Served += n;
            return n;
        }
    }

    // A body that never ends and never sends anything (a stalled stream).
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
    }

    private static HttpResponseMessage Response(string contentType, Stream body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var content = new StreamContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new HttpResponseMessage(status) { Content = content };
    }

    private static LinkPreviewHttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, TimeSpan? bodyTimeout = null) =>
        new(new HttpClient(new StubHandler(respond)), bodyTimeout);

    [Fact]
    public async Task Html_IsRead()
    {
        var html = "<html><head><title>Hi</title></head></html>";
        var client = Client(_ => Response("text/html; charset=utf-8", new MemoryStream(Encoding.UTF8.GetBytes(html))));

        var page = await client.GetAsync("https://example.com/");

        Assert.True(page.IsHtml);
        Assert.Equal(html, Encoding.UTF8.GetString(page.Body));
    }

    // A link to an ISO, a zip or a video used to be downloaded whole, into
    // memory, on every popup open.
    [Fact]
    public async Task NonPreviewableType_IsNotDownloaded()
    {
        var body = new CountingStream(700L * 1024 * 1024);
        var client = Client(_ => Response("application/octet-stream", body));

        var response = await client.GetAsync("https://example.com/big.iso");

        Assert.False(response.IsHtml);
        Assert.False(response.IsImage);
        Assert.Empty(response.Body);
        Assert.True(body.Served < 1024 * 1024);
    }

    // The tags a preview needs are in the <head>: a huge page is cut.
    [Fact]
    public async Task HugeHtml_IsCutAtTheLimit()
    {
        var body = new CountingStream(200L * 1024 * 1024);
        var client = Client(_ => Response("text/html", body));

        var page = await client.GetAsync("https://example.com/huge");

        Assert.Equal(LinkPreviewHttpClient.MaxHtmlBytes, page.Body.Length);
        Assert.True(body.Served < 2L * LinkPreviewHttpClient.MaxHtmlBytes);
    }

    [Fact]
    public async Task HugeImage_IsUnavailable()
    {
        var body = new CountingStream(200L * 1024 * 1024);
        var client = Client(_ => Response("image/png", body));

        await Assert.ThrowsAsync<LinkPreviewUnavailableException>(() => client.GetAsync("https://example.com/big.png"));
        Assert.True(body.Served < 2L * LinkPreviewHttpClient.MaxBodyBytes);
    }

    [Fact]
    public async Task OEmbedJson_IsRead()
    {
        var json = """{"title":"A video"}""";
        var client = Client(_ => Response("application/json", new MemoryStream(Encoding.UTF8.GetBytes(json))));

        var response = await client.GetAsync("https://www.youtube.com/oembed?url=x");

        Assert.Equal(json, Encoding.UTF8.GetString(response.Body));
    }

    // HttpClient.Timeout stops at the headers (ResponseHeadersRead): a body
    // that stalls (a live stream, a dying server) used to be read forever.
    [Fact]
    public async Task StalledBody_GivesUpAfterTheBodyBudget()
    {
        var client = Client(_ => Response("text/html", new StalledStream()), bodyTimeout: TimeSpan.FromMilliseconds(200));

        var fetch = client.GetAsync("https://example.com/stream");
        var finished = await Task.WhenAny(fetch, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(fetch, finished);
        await Assert.ThrowsAsync<LinkPreviewUnavailableException>(() => fetch);
    }

    [Theory]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/x.png")]
    public async Task UnusableUrls_AreUnavailableNotCrashes(string url)
    {
        var client = new LinkPreviewHttpClient(new HttpClient(new StubHandler(_ => throw new NotSupportedException("scheme"))), null);

        await Assert.ThrowsAsync<LinkPreviewUnavailableException>(() => client.GetAsync(url));
    }

    [Fact]
    public async Task ErrorStatus_IsNotRead()
    {
        var body = new CountingStream(50L * 1024 * 1024);
        var client = Client(_ => Response("text/html", body, HttpStatusCode.NotFound));

        var response = await client.GetAsync("https://example.com/missing");

        Assert.False(response.IsSuccess);
        Assert.True(body.Served < 1024 * 1024);
    }
}
