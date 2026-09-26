// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Bounded line extraction for card previews. A card shows a handful of
// lines, but history may hold multi-megabyte texts: splitting the whole
// content (once per converter, per realized card, per refresh) and handing
// a megabyte-long minified line to WPF text layout froze the popup. These
// helpers only walk the start of the text and clip every line.
public static class TextPreview
{
    public const int DefaultMaxLineLength = 400;

    private static readonly char[] LineBreaks = ['\r', '\n'];

    // The first maxLines lines, split on "\r\n", "\r" or "\n" exactly like
    // string.Split with those separators, each clipped to maxLineLength
    // characters (with a trailing "…"). HasMore is true when the text has
    // further lines past the ones returned.
    public static (IReadOnlyList<string> Lines, bool HasMore) FirstLines(
        string? text, int maxLines, int maxLineLength = DefaultMaxLineLength)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0)
        {
            return ([], !string.IsNullOrEmpty(text));
        }
        var lines = new List<string>(Math.Min(maxLines, 16));
        var position = 0;
        while (lines.Count < maxLines)
        {
            var remaining = text.AsSpan(position);
            var breakAt = remaining.IndexOfAny(LineBreaks);
            var length = breakAt < 0 ? remaining.Length : breakAt;
            lines.Add(Clip(text, position, length, maxLineLength));
            if (breakAt < 0)
            {
                return (lines, false);
            }
            position += length;
            position += text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n'
                ? 2
                : 1;
        }
        // A separator followed the last kept line, so at least one more
        // (possibly empty) line exists — string.Split parity.
        return (lines, true);
    }

    // The first line holding non-whitespace, trimmed, as split on '\n'
    // (the card title rule), cut to maxLength characters so a huge first
    // line is never copied whole. Null when every line is blank.
    public static string? FirstNonBlankLine(string? text, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        var position = 0;
        while (position <= text.Length)
        {
            var remaining = text.AsSpan(position);
            var breakAt = remaining.IndexOf('\n');
            var line = (breakAt < 0 ? remaining : remaining[..breakAt]).Trim();
            if (!line.IsEmpty)
            {
                return (line.Length > maxLength ? line[..maxLength] : line).ToString();
            }
            if (breakAt < 0)
            {
                return null;
            }
            position += breakAt + 1;
        }
        return null;
    }

    // The first non-empty '\n'-separated segment, untrimmed: the same value
    // as text.Split('\n', RemoveEmptyEntries).FirstOrDefault() without
    // splitting a multi-thousand-path file list on every converter call.
    public static string? FirstNonEmptySegment(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        var position = 0;
        while (position < text.Length)
        {
            var breakAt = text.IndexOf('\n', position);
            var end = breakAt < 0 ? text.Length : breakAt;
            if (end > position)
            {
                return text[position..end];
            }
            position = end + 1;
        }
        return null;
    }

    private static string Clip(string text, int start, int length, int maxLength)
    {
        if (maxLength <= 0 || length <= maxLength)
        {
            return text.Substring(start, length);
        }
        var cut = maxLength;
        // Never split a surrogate pair: half an emoji renders as a box.
        if (char.IsHighSurrogate(text[start + cut - 1]))
        {
            cut--;
        }
        return string.Concat(text.AsSpan(start, cut), "…");
    }
}
