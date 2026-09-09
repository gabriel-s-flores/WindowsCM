// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Pure seam: exclusion regexes from the link prefs (Copyous `exclusion`
// regex[] parity). Same untrusted-pattern rules as ActionMatcher:
// 2s timeout, bad pattern or timeout = no-match for that pattern.
public sealed class LinkExclusionsTests
{
    [Fact]
    public void IsExcluded_MatchingPattern_ReturnsTrue()
    {
        Assert.True(LinkExclusions.IsExcluded(
            "https://internal.example.com/secret", ["internal\\.example\\.com"]));
    }

    [Fact]
    public void IsExcluded_NoMatch_ReturnsFalse()
    {
        Assert.False(LinkExclusions.IsExcluded(
            "https://example.com/public", ["internal\\.example\\.com"]));
    }

    [Fact]
    public void IsExcluded_NullOrEmptyPatterns_ReturnsFalse()
    {
        Assert.False(LinkExclusions.IsExcluded("https://example.com/", null));
        Assert.False(LinkExclusions.IsExcluded("https://example.com/", []));
    }

    [Fact]
    public void IsExcluded_BadPattern_IsNotExcluded()
    {
        Assert.False(LinkExclusions.IsExcluded("https://example.com/", ["(["]));
    }

    [Fact]
    public void IsExcluded_EmptyUrl_ReturnsFalse()
    {
        Assert.False(LinkExclusions.IsExcluded(string.Empty, [".*"]));
    }
}
