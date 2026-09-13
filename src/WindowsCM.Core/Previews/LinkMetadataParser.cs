// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Previews;

// Pure HTML -> metadata extraction (Copyous link.ts parity, research 01 §8).
// Precedence: og:title | twitter:title | <title>; description mirrors it;
// image: og:image* | twitter:image, resolved relative against the page URL.
// Meta matching accepts property= or name= in either attribute order
// (Copyous "property|name + reverso"); values are HTML-entity decoded.
// Case-insensitive throughout; tolerates broken HTML by regex, like the
// original. The UI layer may parse with AngleSharp instead — observable
// behavior stays identical, so these tests pin the contract, not the engine.
public static partial class LinkMetadataParser
{
    public static LinkMetadata Parse(string? html, string? baseUrl)
    {
        if (string.IsNullOrEmpty(html))
        {
            return new LinkMetadata(null, null, null);
        }

        var metas = ReadMetaTags(html);
        var title = FirstContent(metas, "og:title")
            ?? FirstContent(metas, "twitter:title")
            ?? ReadTitleElement(html);
        var description = FirstContent(metas, "og:description")
            ?? FirstContent(metas, "twitter:description")
            ?? FirstContent(metas, "description");
        var image = FirstImage(metas, html);
        return new LinkMetadata(
            Clean(title),
            Clean(description),
            ResolveImage(image, baseUrl));
    }

    // All <meta> tags as attribute dictionaries (lower-cased names).
    private static List<Dictionary<string, string>> ReadMetaTags(string html)
    {
        var tags = new List<Dictionary<string, string>>();
        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attr in MetaAttrRegex().Matches(tag.Value))
            {
                var name = attr.Groups["name"].Value;
                var value = attr.Groups["dq"].Success ? attr.Groups["dq"].Value
                    : attr.Groups["sq"].Success ? attr.Groups["sq"].Value
                    : attr.Groups["bare"].Value;
                attrs[name] = WebUtility.HtmlDecode(value);
            }
            tags.Add(attrs);
        }
        return tags;
    }

    private static string? FirstContent(
        List<Dictionary<string, string>> metas, string key)
    {
        foreach (var attrs in metas)
        {
            if ((Attr(attrs, "property") ?? Attr(attrs, "name")) is { } kind
                && kind.Equals(key, StringComparison.OrdinalIgnoreCase)
                && Attr(attrs, "content") is { } content
                && content.Trim().Length > 0)
            {
                return content;
            }
        }
        return null;
    }

    // og:image* (og:image, og:image:url, og:image:secure_url) first, then
    // twitter:image, then a plain image/thumbnail meta or <link rel="image_src">
    // (Copyous `image|og:image*` parity). First non-empty wins.
    private static string? FirstImage(List<Dictionary<string, string>> metas, string html)
    {
        string? plain = null;
        string? twitter = null;
        foreach (var attrs in metas)
        {
            if ((Attr(attrs, "property") ?? Attr(attrs, "name")) is not { } kind
                || Attr(attrs, "content") is not { } content
                || content.Trim().Length == 0)
            {
                continue;
            }
            if (kind.Equals("og:image", StringComparison.OrdinalIgnoreCase)
                || kind.StartsWith("og:image:", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }
            twitter ??= kind.Equals("twitter:image", StringComparison.OrdinalIgnoreCase)
                ? content : null;
            plain ??= (kind.Equals("image", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("thumbnail", StringComparison.OrdinalIgnoreCase))
                ? content : null;
        }

        if (twitter is not null) return twitter;
        if (plain is not null) return plain;

        var linkMatch = LinkImageSrcRegex().Match(html);
        if (linkMatch.Success)
        {
            return linkMatch.Groups["href"].Value;
        }

        return null;
    }


    private static string? ReadTitleElement(string html)
    {
        var match = TitleRegex().Matches(html);
        foreach (Match m in match)
        {
            if (m.Groups["t"].Value.Trim().Length > 0)
            {
                return m.Groups["t"].Value;
            }
        }
        return null;
    }

    private static string? Attr(Dictionary<string, string> attrs, string name) =>
        attrs.TryGetValue(name, out var value) ? value : null;

    private static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var decoded = WebUtility.HtmlDecode(value).Trim();
        return decoded.Length == 0 ? null : decoded;
    }

    private static string? ResolveImage(string? image, string? baseUrl)
    {
        var cleaned = Clean(image);
        if (cleaned is null)
        {
            return null;
        }
        if (Uri.TryCreate(cleaned, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }
        if (!string.IsNullOrEmpty(baseUrl)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, cleaned, out var resolved))
        {
            return resolved.AbsoluteUri;
        }
        return cleaned;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(
        @"(?<name>[A-Za-z_:][\w\-.:]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))",
        RegexOptions.IgnoreCase)]
    private static partial Regex MetaAttrRegex();

    [GeneratedRegex(@"<title\b[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<link\b[^>]*\brel\s*=\s*[""']?image_src[""']?[^>]*\bhref\s*=\s*[""'](?<href>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex LinkImageSrcRegex();
}

