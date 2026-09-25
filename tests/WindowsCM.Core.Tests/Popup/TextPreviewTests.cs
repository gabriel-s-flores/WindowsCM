// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Popup;

public sealed class TextPreviewTests
{
    private static readonly string[] Separators = ["\r\n", "\r", "\n"];

    [Theory]
    [InlineData("", 3)]
    [InlineData("single", 3)]
    [InlineData("a\nb\nc", 3)]
    [InlineData("a\nb\nc\nd", 3)]
    [InlineData("a\r\nb\rc\n", 3)]
    [InlineData("a\n", 1)]
    [InlineData("a\n", 8)]
    [InlineData("\n\n\n", 2)]
    [InlineData("x\r\n\r\ny", 2)]
    public void FirstLines_MatchesStringSplit(string text, int maxLines)
    {
        var expected = text.Split(Separators, StringSplitOptions.None);

        var (lines, hasMore) = TextPreview.FirstLines(text, maxLines);

        if (text.Length == 0)
        {
            Assert.Empty(lines);
            Assert.False(hasMore);
            return;
        }
        Assert.Equal(expected.Take(maxLines), lines);
        Assert.Equal(expected.Length > maxLines, hasMore);
    }

    [Fact]
    public void FirstLines_ClipsLongLines_WithoutSplittingSurrogates()
    {
        var longLine = new string('x', 399) + "🚀" + new string('y', 50);

        var (lines, hasMore) = TextPreview.FirstLines(longLine, 8, maxLineLength: 400);

        var line = Assert.Single(lines);
        Assert.False(hasMore);
        Assert.Equal(new string('x', 399) + "…", line);
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("\n   \n  first real line  \nsecond", "first real line")]
    [InlineData("\r\n\t\r\nvalue", "value")]
    [InlineData("   \n\n", null)]
    [InlineData("", null)]
    public void FirstNonBlankLine_MatchesTheTitleRule(string text, string? expected)
    {
        Assert.Equal(expected, TextPreview.FirstNonBlankLine(text));
    }

    [Theory]
    [InlineData("C:\\a.txt\nC:\\b.txt", "C:\\a.txt")]
    [InlineData("\n\nC:\\b.txt\n", "C:\\b.txt")]
    [InlineData("\n\n", null)]
    [InlineData("", null)]
    public void FirstNonEmptySegment_MatchesSplitRemoveEmpty(string text, string? expected)
    {
        Assert.Equal(expected, TextPreview.FirstNonEmptySegment(text));
        Assert.Equal(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            TextPreview.FirstNonEmptySegment(text));
    }

    [Fact]
    public void HugePaste_PreviewTitleAndTokens_StayBounded()
    {
        // A multi-megabyte minified blob: one enormous line, then many more.
        var huge = new string('a', 3_000_000) + "\n" + string.Join("\n", Enumerable.Repeat("line", 200_000));
        var item = new ClipboardItem(ItemKind.Code, huge, false, null, DateTime.UtcNow, null, null);

        var preview = ItemDisplayFormatter.GetPreviewText(item, 8);
        var title = ItemDisplayFormatter.GetTitle(item);
        var tokens = CodeSyntaxTokenizer.Tokenize(huge, 8);

        Assert.True(preview.Length < 8 * (TextPreview.DefaultMaxLineLength + 2) + 8);
        Assert.EndsWith("\n...", preview);
        Assert.Equal(new string('a', 80) + "...", title);
        Assert.True(tokens.Sum(t => t.Text.Length) < 8 * (TextPreview.DefaultMaxLineLength + 2) + 8);
    }

    [Fact]
    public void DetectLanguage_PathologicalInput_ReturnsQuickly()
    {
        // `UPDATE\s+.*\s+SET` over one long line of "update " words used to
        // backtrack for minutes on the UI thread.
        var pathological = string.Concat(Enumerable.Repeat("update x ", 300_000));
        var watch = Stopwatch.StartNew();

        CodeSyntaxTokenizer.DetectLanguage(pathological);

        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
        Assert.Equal("SQL", CodeSyntaxTokenizer.DetectLanguage("UPDATE users SET name = 'x'"));
    }
}
