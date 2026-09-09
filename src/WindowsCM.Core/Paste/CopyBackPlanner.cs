// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Paste;

// Pure copy-back planning: a stored item in, clipboard contents out (Copyous
// `handleEntry` parity). Text-like kinds write verbatim text; file lists
// resolve to local paths with the stored cut/copy marker dropped (the writer
// always forces copy); images load their persisted PNG and convert to DIB.
// Null means "nothing writable" (image file gone, empty file list): the
// orchestrator reports it instead of writing garbage. The image loader is a
// func so this stays pure and disk-free in tests.
public static class CopyBackPlanner
{
    public static ClipboardContents? Plan(ClipboardItem item, Func<string, byte[]?> loadImagePng)
    {
        return item.Kind switch
        {
            ItemKind.Text or ItemKind.Code or ItemKind.Link or ItemKind.Character or ItemKind.Color
                => new ClipboardContents(Text: item.Content, Html: ExtractHtml(item.MetadataJson)),
            ItemKind.File or ItemKind.Files
                => PlanFiles(item.Content),
            ItemKind.Image
                => PlanImage(item.Content, loadImagePng),
            _ => null,
        };
    }

    private static ClipboardContents? PlanFiles(string content)
    {
        var paths = content.Split('\n')
            .Select(ToLocalPath)
            .OfType<string>()
            .ToList();
        return paths.Count == 0 ? null : new ClipboardContents(FileLocalPaths: paths);
    }

    private static ClipboardContents? PlanImage(string content, Func<string, byte[]?> loadImagePng)
    {
        var png = loadImagePng(content);
        if (png is null || png.Length == 0)
        {
            return null;
        }
        byte[]? dib = null;
        try
        {
            dib = PngToDib.FromPng(png);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            // Exotic PNG flavor: the writer falls back to the PNG format.
        }
        return new ClipboardContents(ImagePng: png, ImageDib: dib);
    }

    // CF_HTML stored opaque in v1 (CaptureService parity): only the "html"
    // property is read, everything else (code language ids in issue 14) is
    // left for the owner to merge. Corrupt metadata degrades to text-only.
    private static string? ExtractHtml(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("html", out var html)
                && html.ValueKind == JsonValueKind.String
                ? html.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Stored file content is file:// URIs (Win32ClipboardReader parity), but
    // anything that is not an absolute file URI passes through literally.
    private static string? ToLocalPath(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }
        return trimmed;
    }
}
