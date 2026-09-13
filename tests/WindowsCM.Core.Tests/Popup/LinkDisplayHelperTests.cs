// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Popup;

public sealed class LinkDisplayHelperTests
{
    [Theory]
    [InlineData("https://github.com/boerdereinar/copyous", "github.com")]
    [InlineData("http://www.google.com/search?q=clipboard", "google.com")]
    [InlineData("https://Subdomain.Example.COM/path", "subdomain.example.com")]
    [InlineData("https://news.ycombinator.com:8080/item?id=123", "news.ycombinator.com")]
    [InlineData("github.com/features", "github.com")]
    public void GetDomain_ValidUrls_ExtractsCleanDomainWithoutWww(string input, string expected)
    {
        var domain = LinkDisplayHelper.GetDomain(input);
        Assert.Equal(expected, domain);
    }

    [Fact]
    public void GetDomain_NullOrEmpty_ReturnsFallback()
    {
        Assert.Equal("Link", LinkDisplayHelper.GetDomain(null));
        Assert.Equal("Link", LinkDisplayHelper.GetDomain(""));
        Assert.Equal("Link", LinkDisplayHelper.GetDomain("   "));
    }

    [Fact]
    public void GetFaviconCdnUrl_ValidUrl_GeneratesGoogleFaviconCdnEndpoint()
    {
        var url = "https://github.com/boerdereinar/copyous";
        var cdnUrl = LinkDisplayHelper.GetFaviconCdnUrl(url, 64);
        Assert.Equal("https://www.google.com/s2/favicons?domain=github.com&sz=64", cdnUrl);
    }

    [Fact]
    public void GetFaviconCdnUrl_InvalidUrl_ReturnsEmpty()
    {
        var cdnUrl = LinkDisplayHelper.GetFaviconCdnUrl("not-a-valid-domain", 64);
        Assert.Empty(cdnUrl);
    }

    [Fact]
    public void GetPathOrTitle_WithMetadataTitle_PrefersMetadataTitle()
    {
        var metadata = ItemMetadataJson.EncodeLink("Copyous GitHub Repository", "Clipboard manager", "https://img.png");
        var item = new ClipboardItem(ItemKind.Link, "https://github.com/boerdereinar/copyous", false, null, DateTime.UtcNow, metadata, null);

        var title = LinkDisplayHelper.GetPathOrTitle(item);
        Assert.Equal("Copyous GitHub Repository", title);
    }

    [Fact]
    public void GetPathOrTitle_WithoutMetadataTitle_ExtractsCleanPath()
    {
        var item = new ClipboardItem(ItemKind.Link, "https://github.com/boerdereinar/copyous", false, null, DateTime.UtcNow, null, null);

        var title = LinkDisplayHelper.GetPathOrTitle(item);
        Assert.Equal("boerdereinar/copyous", title);
    }

    [Fact]
    public void GetPathOrTitle_RootUrl_ReturnsHome()
    {
        var item = new ClipboardItem(ItemKind.Link, "https://github.com/", false, null, DateTime.UtcNow, null, null);

        var title = LinkDisplayHelper.GetPathOrTitle(item);
        Assert.Equal("Página Inicial", title);
    }

    [Theory]
    [InlineData("https://github.com/boerdereinar/copyous/", "github.com/boerdereinar/copyous")]
    [InlineData("http://google.com/", "google.com")]
    public void GetDisplayUrl_StripsSchemeAndTrailingSlash(string input, string expected)
    {
        var display = LinkDisplayHelper.GetDisplayUrl(input);
        Assert.Equal(expected, display);
    }

    [Fact]
    public void GetDescription_WithMetadata_ReturnsExtractedDescription()
    {
        var metadata = ItemMetadataJson.EncodeLink("Title", "A great clipboard manager", "https://img.png");
        var item = new ClipboardItem(ItemKind.Link, "https://example.com", false, null, DateTime.UtcNow, metadata, null);

        var desc = LinkDisplayHelper.GetDescription(item);
        Assert.Equal("A great clipboard manager", desc);
    }

    [Fact]
    public void GetDescription_WithoutMetadata_ReturnsEmpty()
    {
        var item = new ClipboardItem(ItemKind.Link, "https://example.com", false, null, DateTime.UtcNow, null, null);

        var desc = LinkDisplayHelper.GetDescription(item);
        Assert.Equal("", desc);
    }

    [Fact]
    public void HasPreviewImage_WithValidUri_ReturnsTrue()
    {
        var metadata = ItemMetadataJson.EncodeLink("Title", "Desc", "https://example.com/thumb.jpg");
        var item = new ClipboardItem(ItemKind.Link, "https://example.com", false, null, DateTime.UtcNow, metadata, null);

        Assert.True(LinkDisplayHelper.HasPreviewImage(item));
    }

    [Fact]
    public void HasPreviewImage_WithoutImage_ReturnsFalse()
    {
        var metadata = ItemMetadataJson.EncodeLink("Title", "Desc", null);
        var item = new ClipboardItem(ItemKind.Link, "https://example.com", false, null, DateTime.UtcNow, metadata, null);

        Assert.False(LinkDisplayHelper.HasPreviewImage(item));
    }
}
