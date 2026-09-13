// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using WindowsCM.Core.History;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Popup;

// Pure helper for link presentation, domain extraction, and favicon endpoint resolution.
public static class LinkDisplayHelper
{
    public static string GetDomain(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return "Link";
        }

        var trimmed = rawUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            // Try prefixing https:// if user copied domain directly
            if (Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
            {
                // check if valid host
                if (!string.IsNullOrEmpty(uri.Host) && uri.Host.Contains('.'))
                {
                    return CleanHost(uri.Host);
                }
            }
            return trimmed.Length > 30 ? trimmed[..30] + "..." : trimmed;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return trimmed;
        }

        return CleanHost(uri.Host);
    }

    public static string GetFaviconCdnUrl(string? rawUrl, int size = 64)
    {
        var domain = GetDomain(rawUrl);
        if (string.IsNullOrWhiteSpace(domain) || domain == "Link" || !domain.Contains('.'))
        {
            return "";
        }
        return $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz={size}";
    }

    public static string GetPathOrTitle(ClipboardItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title.Trim();
        }

        // Check if metadata contains a title from link preview
        var (title, _, _) = ItemMetadataJson.GetLink(item.MetadataJson);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var trimmed = (item.Content ?? "").Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath?.Trim('/') ?? "";
            if (string.IsNullOrEmpty(path))
            {
                return "Página Inicial";
            }
            var decoded = Uri.UnescapeDataString(path);
            return decoded.Length > 50 ? decoded[..50] + "..." : decoded;
        }

        return "Website";
    }

    public static string GetDisplayUrl(string? rawUrl, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return "";
        }

        var trimmed = rawUrl.Trim();
        var display = trimmed;
        if (display.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            display = display[8..];
        }
        else if (display.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            display = display[7..];
        }

        if (display.EndsWith('/'))
        {
            display = display[..^1];
        }

        return display.Length > maxLength ? display[..maxLength] + "..." : display;
    }

    public static string GetDescription(ClipboardItem item)
    {
        var (_, desc, _) = ItemMetadataJson.GetLink(item.MetadataJson);
        return desc?.Trim() ?? "";
    }

    public static bool HasPreviewImage(ClipboardItem item)
    {
        var (_, _, img) = ItemMetadataJson.GetLink(item.MetadataJson);
        if (string.IsNullOrWhiteSpace(img))
        {
            return false;
        }
        return File.Exists(img) || Uri.TryCreate(img, UriKind.Absolute, out _);
    }

    private static string CleanHost(string host)
    {
        var lower = host.ToLowerInvariant();
        if (lower.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            lower = lower[4..];
        }
        return lower;
    }
}
