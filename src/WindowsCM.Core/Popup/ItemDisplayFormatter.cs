// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Text.Json;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Popup;

// Formatter for popup card representation (Copyous parity + Windows 11 Fluent).
// Pure logic: extracts clean titles (hiding internal file:// and absolute disk
// paths), human-readable kind labels, multiline previews, and resolves image paths.
public static class ItemDisplayFormatter
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".tiff"
    };

    public static string GetTitle(ClipboardItem item) =>
        GetTitle(item, null);

    public static string GetTitle(ClipboardItem item, bool? isPortuguese)
    {
        var isPt = isPortuguese ?? Localization.LocalizationManager.IsPortuguese;
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title.Trim();
        }

        switch (item.Kind)
        {
            case ItemKind.Image:
                return isPt ? "Imagem" : "Image";

            case ItemKind.File:
            {
                var path = NormalizePath(item.Content?.Split('\n').FirstOrDefault());
                if (string.IsNullOrWhiteSpace(path))
                {
                    return isPt ? "Arquivo" : "File";
                }
                var fileName = Path.GetFileName(path);
                return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
            }

            case ItemKind.Files:
            {
                var paths = (item.Content ?? "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizePath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (paths.Count == 0)
                {
                    return isPt ? "Arquivos" : "Files";
                }
                if (paths.Count == 1)
                {
                    return Path.GetFileName(paths[0]);
                }
                var first = Path.GetFileName(paths[0]);
                return isPt ? $"{paths.Count} arquivos ({first})" : $"{paths.Count} files ({first})";
            }

            case ItemKind.Code:
            case ItemKind.Text:
            {
                var line = (item.Content ?? "")
                    .Split('\n')
                    .Select(s => s.Trim())
                    .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "";
                if (line.Length > 80)
                {
                    return line[..80] + "...";
                }
                if (line.Length > 0)
                {
                    return line;
                }
                if (item.Kind == ItemKind.Code)
                {
                    return isPt ? "Código" : "Code";
                }
                return isPt ? "Texto" : "Text";
            }

            case ItemKind.Color:
                return item.Content?.Trim() ?? (isPt ? "Cor" : "Color");

            case ItemKind.Character:
            {
                var content = item.Content?.Trim() ?? "";
                if (IsEmoji(content))
                {
                    return "Emoji";
                }
                return string.IsNullOrEmpty(content) ? (isPt ? "Caractere" : "Character") : content;
            }

            case ItemKind.Link:
            {
                var trimmed = item.Content?.Trim() ?? "";
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
                var line = trimmed.Split('\n').FirstOrDefault()?.Trim() ?? "";
                return line.Length > 80 ? line[..80] + "..." : (line.Length > 0 ? line : "Link");
            }

            default:
                return item.Kind.ToString();
        }
    }

    public static string GetTypeLabel(ClipboardItem item, FileCategorySettings? categorySettings = null) =>
        GetTypeLabel(item, categorySettings, null);

    public static string GetTypeLabel(ClipboardItem item, FileCategorySettings? categorySettings, bool? isPortuguese)
    {
        var isPt = isPortuguese ?? Localization.LocalizationManager.IsPortuguese;
        switch (item.Kind)
        {
            case ItemKind.Image:
                return isPt ? "Imagem PNG" : "PNG Image";

            case ItemKind.File:
            {
                var path = NormalizePath(item.Content?.Split('\n').FirstOrDefault());
                var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                if (categorySettings != null)
                {
                    var cat = categorySettings.ResolveCategory(ext);
                    if (cat != null && !cat.IsBuiltIn)
                    {
                        return string.IsNullOrEmpty(ext) ? cat.Name : $"{cat.Name} • {ext.TrimStart('.').ToUpperInvariant()}";
                    }
                }
                return WindowsFileTypeResolver.GetFriendlyExtensionLabel(ext, isPt);
            }

            case ItemKind.Files:
            {
                var count = (item.Content ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                if (isPt)
                {
                    return count > 1 ? $"{count} arquivos" : "Múltiplos arquivos";
                }
                return count > 1 ? $"{count} files" : "Multiple files";
            }

            case ItemKind.Code:
            {
                var lang = ExtractLanguageFromMetadata(item.MetadataJson);
                if (string.IsNullOrEmpty(lang))
                {
                    lang = Previews.CodeSyntaxTokenizer.DetectLanguage(item.Content);
                }
                var codeWord = isPt ? "Código" : "Code";
                return !string.IsNullOrEmpty(lang) ? $"{codeWord} ({lang})" : codeWord;
            }

            case ItemKind.Text:
            {
                var len = item.Content?.Length ?? 0;
                if (isPt)
                {
                    return len > 0 ? $"Texto • {len} caracteres" : "Texto";
                }
                return len > 0 ? $"Text • {len} characters" : "Text";
            }

            case ItemKind.Link:
                return isPt ? "Link Web" : "Web Link";

            case ItemKind.Color:
                return isPt ? "Cor" : "Color";

            case ItemKind.Character:
            {
                var content = item.Content?.Trim() ?? "";
                if (IsEmoji(content))
                {
                    var count = EmojiDetector.CountEmojis(content);
                    if (count > 1)
                    {
                        return $"Emoji • {count} emojis";
                    }
                    var code = GetUnicodeCodePoint(content);
                    return !string.IsNullOrEmpty(code) ? $"Emoji • {code}" : "Emoji";
                }
                var charCode = GetUnicodeCodePoint(content);
                var charWord = isPt ? "Caractere" : "Character";
                return !string.IsNullOrEmpty(charCode) ? $"{charWord} • {charCode}" : charWord;
            }

            default:
                return item.Kind.ToString();
        }
    }

    public static string GetPreviewText(ClipboardItem item, int maxLines = 8)
    {
        if (string.IsNullOrEmpty(item.Content))
        {
            return "";
        }
        var lines = item.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var previewLines = lines.Take(maxLines).ToList();
        var result = string.Join("\n", previewLines);
        if (lines.Length > maxLines)
        {
            result += "\n...";
        }
        return result;
    }

    public static string? TryGetLocalImagePath(ClipboardItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Content))
        {
            return null;
        }

        if (item.Kind == ItemKind.Image)
        {
            var uriStr = item.Content.Trim();
            if (uriStr.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var local = new Uri(uriStr).LocalPath;
                    return File.Exists(local) ? local : null;
                }
                catch
                {
                    return null;
                }
            }
            return File.Exists(uriStr) ? uriStr : null;
        }

        if (item.Kind == ItemKind.File)
        {
            var firstLine = item.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var path = NormalizePath(firstLine);
            if (!string.IsNullOrWhiteSpace(path) && ImageExtensions.Contains(Path.GetExtension(path)))
            {
                return File.Exists(path) ? path : null;
            }
        }

        return null;
    }

    public static bool IsEmoji(string? text) => EmojiDetector.IsAllEmojis(text);

    public static string GetUnicodeCodePoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var runes = text.EnumerateRunes().Take(4).ToList();
        return string.Join(" ", runes.Select(r => $"U+{r.Value:X4}"));
    }

    public static string GetKindIconGlyph(ItemKind kind, string? content = null, FileCategorySettings? categories = null)
    {
        if ((kind == ItemKind.File || kind == ItemKind.Files) && categories != null && !string.IsNullOrWhiteSpace(content))
        {
            var first = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var cat = categories.ResolveCategory(first);
            if (cat != null)
            {
                return cat.Id switch
                {
                    "images" => "\uEB9F",
                    "video" => "\uE714",
                    "audio" => "\uEC4F",
                    "documents" => "\uE8A5",
                    "spreadsheets" => "\uF0E3",
                    "presentations" => "\uE8AD",
                    "code" => "\uE943",
                    "archives" => "\uF012",
                    _ => "\uED43"
                };
            }
        }

        return kind switch
        {
            ItemKind.Code => "\uE943",
            ItemKind.Text => "\uE8A5",
            ItemKind.Image => "\uEB9F",
            ItemKind.File => "\uED43",
            ItemKind.Files => "\uED25",
            ItemKind.Link => "\uE71B",
            ItemKind.Color => "\uE790",
            ItemKind.Character => IsEmoji(content) ? "\uED53" : "\uE76E",
            _ => "\uE8A5"
        };
    }

    private static string NormalizePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(trimmed).LocalPath;
            }
            catch
            {
                return trimmed;
            }
        }
        return trimmed;
    }

    private static string? ExtractLanguageFromMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("language", out var langProp))
            {
                if (langProp.ValueKind == JsonValueKind.Object && langProp.TryGetProperty("name", out var nameProp))
                {
                    return nameProp.GetString();
                }
                if (langProp.ValueKind == JsonValueKind.String)
                {
                    return langProp.GetString();
                }
            }
        }
        catch
        {
        }
        return null;
    }
}
