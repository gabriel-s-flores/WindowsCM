// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Text.Json;
using WindowsCM.Core.History;

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

    public static string GetTitle(ClipboardItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title.Trim();
        }

        switch (item.Kind)
        {
            case ItemKind.Image:
                return "Imagem";

            case ItemKind.File:
            {
                var path = NormalizePath(item.Content?.Split('\n').FirstOrDefault());
                if (string.IsNullOrWhiteSpace(path))
                {
                    return "Arquivo";
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
                    return "Arquivos";
                }
                if (paths.Count == 1)
                {
                    return Path.GetFileName(paths[0]);
                }
                var first = Path.GetFileName(paths[0]);
                return $"{paths.Count} arquivos ({first})";
            }

            case ItemKind.Code:
            case ItemKind.Text:
            {
                var line = (item.Content ?? "")
                    .Split('\n')
                    .Select(s => s.Trim())
                    .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "";
                return line.Length > 80 ? line[..80] + "..." : (line.Length > 0 ? line : (item.Kind == ItemKind.Code ? "Código" : "Texto"));
            }

            case ItemKind.Color:
                return item.Content?.Trim() ?? "Cor";

            case ItemKind.Character:
            {
                var content = item.Content?.Trim() ?? "";
                if (IsEmoji(content))
                {
                    return "Emoji";
                }
                return string.IsNullOrEmpty(content) ? "Caractere" : content;
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

    public static string GetTypeLabel(ClipboardItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Image:
                return "Imagem PNG";

            case ItemKind.File:
            {
                var path = NormalizePath(item.Content?.Split('\n').FirstOrDefault());
                var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                return ext switch
                {
                    ".png" => "Imagem PNG",
                    ".jpg" or ".jpeg" => "Imagem JPEG",
                    ".gif" => "Imagem GIF",
                    ".webp" => "Imagem WebP",
                    ".bmp" => "Imagem BMP",
                    ".svg" => "Imagem Vetorial SVG",
                    ".ico" => "Ícone ICO",
                    ".mp4" => "Vídeo MP4",
                    ".mkv" => "Vídeo MKV",
                    ".avi" => "Vídeo AVI",
                    ".mov" => "Vídeo MOV",
                    ".webm" => "Vídeo WebM",
                    ".mp3" => "Áudio MP3",
                    ".wav" => "Áudio WAV",
                    ".flac" => "Áudio FLAC",
                    ".pdf" => "Documento PDF",
                    ".docx" or ".doc" => "Documento Word",
                    ".xlsx" or ".xls" => "Planilha Excel",
                    ".pptx" or ".ppt" => "Apresentação PowerPoint",
                    ".zip" or ".rar" or ".7z" => "Arquivo Compactado",
                    ".cs" => "Código C#",
                    ".js" => "Código JavaScript",
                    ".ts" => "Código TypeScript",
                    ".py" => "Código Python",
                    ".json" => "Arquivo JSON",
                    ".xml" => "Arquivo XML",
                    ".html" or ".htm" => "Documento HTML",
                    ".css" => "Folha de Estilos CSS",
                    ".md" => "Documento Markdown",
                    ".txt" => "Documento de Texto",
                    _ when !string.IsNullOrEmpty(ext) => $"Arquivo {ext.TrimStart('.').ToUpperInvariant()}",
                    _ => "Arquivo"
                };
            }

            case ItemKind.Files:
            {
                var count = (item.Content ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                return count > 1 ? $"{count} arquivos" : "Múltiplos arquivos";
            }

            case ItemKind.Code:
            {
                var lang = ExtractLanguageFromMetadata(item.MetadataJson);
                if (string.IsNullOrEmpty(lang))
                {
                    var detected = Previews.CodeSyntaxTokenizer.DetectLanguage(item.Content);
                    if (detected != "Código")
                    {
                        lang = detected;
                    }
                }
                return !string.IsNullOrEmpty(lang) ? $"Código ({lang})" : "Código";
            }

            case ItemKind.Text:
            {
                var len = item.Content?.Length ?? 0;
                return len > 0 ? $"Texto • {len} caracteres" : "Texto";
            }

            case ItemKind.Link:
                return "Link Web";

            case ItemKind.Color:
                return "Cor";

            case ItemKind.Character:
            {
                var content = item.Content?.Trim() ?? "";
                var code = GetUnicodeCodePoint(content);
                var prefix = IsEmoji(content) ? "Emoji" : "Caractere";
                return !string.IsNullOrEmpty(code) ? $"{prefix} • {code}" : prefix;
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

    public static bool IsEmoji(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var rune in text.EnumerateRunes())
        {
            var val = rune.Value;
            if ((val >= 0x1F300 && val <= 0x1FAFF) ||
                (val >= 0x2600 && val <= 0x27BF) ||
                (val >= 0xFE00 && val <= 0xFE0F) ||
                (val >= 0x1F1E6 && val <= 0x1F1FF) ||
                (val >= 0x200D && val <= 0x200D))
            {
                return true;
            }
        }
        return false;
    }

    public static string GetUnicodeCodePoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var runes = text.EnumerateRunes().Take(4).ToList();
        return string.Join(" ", runes.Select(r => $"U+{r.Value:X4}"));
    }

    public static string GetKindIconGlyph(ItemKind kind, string? content = null) => kind switch
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
