// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsCM.Core.Release;
using Xunit;

namespace WindowsCM.Core.Tests.Release;

public class GitHubUpdateServiceTests
{
    private const string Hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Theory]
    [InlineData(true, "WindowsCM-Setup-1.2.3.exe")]
    [InlineData(false, "WindowsCM-portable-1.2.3.zip")]
    public async Task SelectsCorrectAssetAndChecksum(bool installed, string name)
    {
        using var client = Client("v1.2.3", name);
        var update = await new GitHubUpdateService(client).CheckAsync(new Version(1, 0, 0), installed, default);
        Assert.NotNull(update);
        Assert.Equal(name, update.AssetName);
        Assert.Equal(Hash, update.Sha256);
    }

    [Theory]
    [InlineData("v1.0.0", false, false)]
    [InlineData("v0.9.0", false, false)]
    [InlineData("v1.2.3-beta", false, false)]
    [InlineData("v1.2.3", true, false)]
    [InlineData("v1.2.3", false, true)]
    public async Task DoesNotOfferOldInvalidOrUnstableReleases(string tag, bool draft, bool prerelease)
    {
        using var client = Client(tag, "WindowsCM-Setup-1.2.3.exe", draft, prerelease);
        Assert.Null(await new GitHubUpdateService(client).CheckAsync(new Version(1, 0, 0), true, default));
    }

    [Fact]
    public async Task MissingAssetDoesNotOfferIncompleteRelease()
    {
        using var client = Client("v1.2.3", "unrelated.exe");
        Assert.Null(await new GitHubUpdateService(client).CheckAsync(new Version(1, 0, 0), true, default));
    }

    [Theory]
    [InlineData("https://evil.example/payload.exe")]
    [InlineData("http://github.com/gabriel-s-flores/WindowsCM/releases/download/v1.2.3/file.exe")]
    [InlineData("https://github.com/another/repo/releases/download/v1.2.3/file.exe")]
    public void RejectsForeignAssetUrls(string url) => Assert.False(GitHubUpdateService.IsReleaseAsset(new Uri(url), "v1.2.3"));

    [Fact]
    public void ChecksumRequiresExactNameAndValidHash()
    {
        Assert.Equal(Hash, GitHubUpdateService.ParseChecksum(Hash + "  file.zip\r\n", "file.zip"));
        Assert.Null(GitHubUpdateService.ParseChecksum(Hash + "  other.zip", "file.zip"));
        Assert.Null(GitHubUpdateService.ParseChecksum("invalid  file.zip", "file.zip"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DownloadChecksHashAndDeletesCorruptPayload(bool valid)
    {
        var bytes = Encoding.UTF8.GetBytes("update fixture");
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        var path = Path.Combine(Path.GetTempPath(), "WindowsCM-update-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var update = new AvailableUpdate(new Version(1, 2, 3), "file.zip", new Uri("https://github.com/test"),
                valid ? Convert.ToHexString(SHA256.HashData(bytes)) : Hash);
            if (valid)
            {
                await new GitHubUpdateService(client).DownloadAsync(update, path, default);
                Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            }
            else
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(client).DownloadAsync(update, path, default));
                Assert.False(File.Exists(path));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task NoReleaseReturnsNoUpdate()
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await new GitHubUpdateService(client).CheckAsync(new Version(1, 0, 0), true, default));
    }

    private static HttpClient Client(string tag, string name, bool draft = false, bool prerelease = false) =>
        new(new Handler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.Host == "api.github.com" ? JsonSerializer.Serialize(new
            {
                tag_name = tag, draft, prerelease,
                assets = new[] { name, "SHA256SUMS.txt" }.Select(n => new { name = n,
                    browser_download_url = $"https://github.com/{GitHubUpdateService.Repository}/releases/download/{tag}/{n}" })
            }) : Hash + "  " + name)
        }));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
