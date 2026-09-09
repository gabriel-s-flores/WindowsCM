// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Classification;

// Link detection (Copyous parity: trimmed text starting with `http` that is
// a valid absolute http(s) URI).
public static class LinkDetector
{
    public static bool IsLink(string? trimmed)
    {
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("http", StringComparison.Ordinal))
        {
            return false;
        }
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
