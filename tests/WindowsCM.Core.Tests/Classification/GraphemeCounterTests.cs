// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class GraphemeCounterTests
{
    [Theory]
    [InlineData("hello", 5)]
    [InlineData("a", 1)]
    [InlineData("", 0)]
    [InlineData("😀", 1)]
    [InlineData("é", 1)]
    [InlineData("🇫🇮", 1)]
    [InlineData("👨‍👩‍👧‍👦", 1)]
    public void Count_MatchesVisibleCharacters(string text, int expected)
    {
        Assert.Equal(expected, GraphemeCounter.Count(text));
    }

    [Theory]
    [InlineData("ab", 1, true)]
    [InlineData("a", 1, false)]
    [InlineData("", 1, false)]
    [InlineData("😀", 1, false)]
    [InlineData("😀😀", 1, true)]
    public void HasMoreThan_BoundsGraphemes(string text, int max, bool expected)
    {
        Assert.Equal(expected, GraphemeCounter.HasMoreThan(text, max));
    }
}
