// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class LinkDetectorTests
{
    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com/path?q=1", true)]
    [InlineData("http", false)]
    [InlineData("httpnotaurl", false)]
    [InlineData("https://", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("just text", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLink_HttpPrefixPlusValidUri(string? text, bool expected)
    {
        Assert.Equal(expected, LinkDetector.IsLink(text));
    }

    [Fact]
    public void IsLink_UppercaseScheme_False()
    {
        // Parity with Copyous `trimmed.startsWith('http')`: case-sensitive.
        Assert.False(LinkDetector.IsLink("HTTP://EXAMPLE.COM"));
    }
}
