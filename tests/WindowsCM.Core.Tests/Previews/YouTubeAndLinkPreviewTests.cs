// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using WindowsCM.Core.History;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

public sealed class YouTubeAndLinkPreviewTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&t=42s", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void TryExtractYouTubeVideoId_ValidUrls_ExtractsExpectedId(string url, string expectedId)
    {
        var success = LinkPreviewService.TryExtractYouTubeVideoId(url, out var videoId);

        Assert.True(success);
        Assert.Equal(expectedId, videoId);
    }

    [Theory]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://notyoutube.com/something")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void TryExtractYouTubeVideoId_NonYouTubeUrls_ReturnsFalse(string? url)
    {
        var success = LinkPreviewService.TryExtractYouTubeVideoId(url!, out var videoId);

        Assert.False(success);
        Assert.Null(videoId);
    }

    [Fact]
    public async Task FetchAsync_YouTubeUrl_FetchesOEmbedAndCachesThumbnail()
    {
        const string videoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        const string oembedUrl = "https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3DdQw4w9WgXcQ&format=json";
        const string thumbUrl = "https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg";

        var http = new FakeLinkPreviewHttp();
        http.Pages[oembedUrl] = new LinkHttpResponse(
            "application/json",
            Encoding.UTF8.GetBytes("""{"title":"Rick Astley - Never Gonna Give You Up","author_name":"Rick Astley","thumbnail_url":"https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg"}"""));
        http.Images[thumbUrl] = [1, 2, 3, 4];

        var cache = new FakeLinkImageCache();
        var service = new LinkPreviewService(http, cache, new LinkPreviewOptions());

        var result = await service.FetchAsync(videoUrl);

        Assert.NotNull(result);
        Assert.Equal("Rick Astley - Never Gonna Give You Up", result.Metadata.Title);
        Assert.Equal("Rick Astley", result.Metadata.Description);
        Assert.Equal(thumbUrl, result.Metadata.ImageUrl);
        Assert.NotNull(result.CachedImagePath);
        Assert.True(cache.Files.ContainsKey(videoUrl));
    }

    [Fact]
    public async Task FetchAsync_YouTubeUrl_OEmbedFails_FallsBackToDefaultThumbnail()
    {
        const string videoUrl = "https://youtu.be/dQw4w9WgXcQ";
        const string thumbUrl = "https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg";

        var http = new FakeLinkPreviewHttp();
        // oembed not in http.Pages => will throw LinkPreviewUnavailableException in stub
        http.Images[thumbUrl] = [5, 6, 7];

        var cache = new FakeLinkImageCache();
        var service = new LinkPreviewService(http, cache, new LinkPreviewOptions());

        var result = await service.FetchAsync(videoUrl);

        Assert.NotNull(result);
        Assert.Equal("Vídeo do YouTube", result.Metadata.Title);
        Assert.Equal("YouTube", result.Metadata.Description);
        Assert.Equal(thumbUrl, result.Metadata.ImageUrl);
        Assert.NotNull(result.CachedImagePath);
    }

    [Fact]
    public void SqliteHistoryStore_SetMetadataAndTitle_UpdatesStoredRecord()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var item = store.AddOrUpdate(new ClipboardItem(
            ItemKind.Link,
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            false,
            null,
            DateTime.UtcNow,
            null,
            null));

        const string newMeta = "{\"title\":\"Rick Astley\",\"description\":\"Never Gonna Give You Up\",\"image\":\"test.jpg\"}";
        store.SetMetadataAndTitle(item.Id, newMeta, "Rick Astley");

        var updated = store.List().FirstOrDefault(i => i.Id == item.Id);
        Assert.NotNull(updated);
        Assert.Equal(newMeta, updated.MetadataJson);
        Assert.Equal("Rick Astley", updated.Title);
    }
}
