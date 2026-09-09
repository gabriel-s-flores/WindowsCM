// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Pure parser seam (spec Testing Decisions): no HTTP, no disk, no mocks.
// Mirrors the observable AngleSharp behavior (og:*/twitter:*/<title> with
// relative resolution) so the UI layer can swap in AngleSharp without
// changing test expectations.
public sealed class LinkMetadataParserTests
{
    private const string Base = "https://example.com/articles/1";

    [Fact]
    public void Parse_OgTags_WinsOverTitleElement()
    {
        const string html = """
            <html><head>
            <title>Fallback Title</title>
            <meta property="og:title" content="OG Title" />
            <meta property="og:description" content="OG Desc" />
            <meta property="og:image" content="/img/og.png" />
            </head></html>
            """;

        var meta = LinkMetadataParser.Parse(html, Base);

        Assert.Equal("OG Title", meta.Title);
        Assert.Equal("OG Desc", meta.Description);
        Assert.Equal("https://example.com/img/og.png", meta.ImageUrl);
    }

    [Fact]
    public void Parse_TwitterTags_UsedWhenNoOg()
    {
        const string html = """
            <html><head>
            <meta name="twitter:title" content="Tw Title" />
            <meta name="twitter:description" content="Tw Desc" />
            <meta name="twitter:image" content="https://cdn.example.com/tw.png" />
            </head></html>
            """;

        var meta = LinkMetadataParser.Parse(html, Base);

        Assert.Equal("Tw Title", meta.Title);
        Assert.Equal("Tw Desc", meta.Description);
        Assert.Equal("https://cdn.example.com/tw.png", meta.ImageUrl);
    }

    [Fact]
    public void Parse_TitleElement_UsedAsFallback()
    {
        const string html = "<html><head><title>  Plain &amp; Simple  </title></head></html>";

        var meta = LinkMetadataParser.Parse(html, Base);

        Assert.Equal("Plain & Simple", meta.Title);
        Assert.Null(meta.Description);
        Assert.Null(meta.ImageUrl);
    }

    [Fact]
    public void Parse_ReversedMetaAttributes_StillMatches()
    {
        const string html = "<html><head>" +
            "<meta content=\"Reversed\" property=\"og:title\" />" +
            "</head></html>";

        var meta = LinkMetadataParser.Parse(html, Base);

        Assert.Equal("Reversed", meta.Title);
    }

    [Fact]
    public void Parse_HtmlEntities_Decoded()
    {
        const string html = "<html><head>" +
            "<meta property=\"og:description\" content=\"a &lt;b&gt; &quot;q&quot; &#39;s&#39;\" />" +
            "</head></html>";

        var meta = LinkMetadataParser.Parse(html, Base);

        Assert.Equal("a <b> \"q\" 's'", meta.Description);
    }

    [Fact]
    public void Parse_EmptyHtml_ReturnsEmptyMetadata()
    {
        var meta = LinkMetadataParser.Parse(string.Empty, Base);

        Assert.Null(meta.Title);
        Assert.Null(meta.Description);
        Assert.Null(meta.ImageUrl);
        Assert.True(meta.IsEmpty);
    }

    [Fact]
    public void Parse_RelativeImage_ResolvedAgainstBase()
    {
        const string html = "<html><head>" +
            "<meta property=\"og:image\" content=\"images/rel.png\" />" +
            "</head></html>";

        var meta = LinkMetadataParser.Parse(html, "https://example.com/a/b/page");

        Assert.Equal("https://example.com/a/b/images/rel.png", meta.ImageUrl);
    }
}
