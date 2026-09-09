// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Actions;

// The Windows behavior of paste-as-path (research 05 §2.1): stored file
// content is file:// URIs, but pasting wants local paths. One path per
// line for Files items. Pure so the executor and any future UI share it.
public static class PathRewriter
{
    // `Uri.UnescapeDataString(s.Replace("file://","").Trim().Trim('/'))`
    // in research order: strip the scheme, trim whitespace, trim slashes
    // (leading "/" from file:///C:/… and any trailing one), then unescape.
    // Separators are preserved verbatim (no backslash normalization):
    // file:///C:/a.txt becomes C:/a.txt, which Win32 and Explorer accept
    // everywhere these results go (open, reveal, paste).
    // Unescaping last mirrors the spec expression; a malformed escape
    // falls back to the pre-unescape text rather than throwing.
    public static string ToLocalPath(string line)
    {
        var stripped = line.Replace("file://", string.Empty, StringComparison.Ordinal)
            .Trim()
            .Trim('/');
        try
        {
            return Uri.UnescapeDataString(stripped);
        }
        catch (UriFormatException)
        {
            return stripped;
        }
    }

    // Multi-line content (Files items): every non-blank line rewrites,
    // blanks drop out. Empty result means nothing pastable.
    public static string ToLocalPaths(string content)
    {
        var paths = content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(ToLocalPath)
            .ToList();
        return string.Join("\n", paths);
    }
}
