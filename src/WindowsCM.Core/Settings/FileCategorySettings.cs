// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;

namespace WindowsCM.Core.Settings;

public sealed class FileCategory
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
    public bool IsBuiltIn { get; set; } = false;

    public FileCategory() { }

    public FileCategory(string id, string name, string colorHex, IEnumerable<string> extensions, bool isBuiltIn = false)
    {
        Id = id;
        Name = name;
        ColorHex = ItemColorSettings.NormalizeHex(colorHex) ?? colorHex;
        Extensions = extensions.Select(NormalizeExtension).Where(e => !string.IsNullOrEmpty(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        IsBuiltIn = isBuiltIn;
    }

    public static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return "";
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}

public sealed class FileCategorySettings
{
    public List<FileCategory> Categories { get; set; } = [];

    public FileCategorySettings()
    {
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        Categories = CreateDefaultCategories();
    }

    public FileCategory? ResolveCategory(string? extensionOrPath)
    {
        if (string.IsNullOrWhiteSpace(extensionOrPath)) return null;

        var ext = Path.GetExtension(extensionOrPath.Trim());
        if (string.IsNullOrEmpty(ext))
        {
            ext = FileCategory.NormalizeExtension(extensionOrPath);
        }
        else
        {
            ext = ext.ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(ext) || ext == ".") return null;

        return Categories.FirstOrDefault(c =>
            c.Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)));
    }

    public FileCategory AddCategory(string name, string colorHex, IEnumerable<string> extensions)
    {
        var id = "custom_" + Guid.NewGuid().ToString("N")[..8];
        var category = new FileCategory(id, name, colorHex, extensions, isBuiltIn: false);
        Categories.Add(category);
        return category;
    }

    public bool RemoveCategory(string categoryId)
    {
        var category = Categories.FirstOrDefault(c => c.Id == categoryId);
        if (category == null || category.IsBuiltIn)
        {
            return false;
        }

        return Categories.Remove(category);
    }

    public void UpdateCategory(string categoryId, string? newName, string? newColorHex, IEnumerable<string>? newExtensions)
    {
        var category = Categories.FirstOrDefault(c => c.Id == categoryId);
        if (category == null) return;

        if (!string.IsNullOrWhiteSpace(newName))
        {
            category.Name = newName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(newColorHex))
        {
            category.ColorHex = ItemColorSettings.NormalizeHex(newColorHex) ?? category.ColorHex;
        }

        if (newExtensions != null)
        {
            category.Extensions = newExtensions
                .Select(FileCategory.NormalizeExtension)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public void Clamp()
    {
        foreach (var cat in Categories)
        {
            var normalized = ItemColorSettings.NormalizeHex(cat.ColorHex);
            if (cat.Id == "images" && (normalized == null || string.Equals(normalized, "#B146C2", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "#107C41", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#16A34A";
            }
            else if (cat.Id == "code" && (normalized == null || string.Equals(normalized, "#5A62D6", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "#7B39ED", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#6366F1";
            }
            else if (cat.Id == "audio" && (normalized == null || string.Equals(normalized, "#E81123", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#8B5CF6";
            }
            else if (cat.Id == "spreadsheets" && (normalized == null || string.Equals(normalized, "#107C41", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#0D9488";
            }
            else if (cat.Id == "video" && (normalized == null || string.Equals(normalized, "#D13438", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#DC2626";
            }
            else if (cat.Id == "presentations" && (normalized == null || string.Equals(normalized, "#D83B01", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#EA580C";
            }
            else if (cat.Id == "archives" && (normalized == null || string.Equals(normalized, "#CA5010", StringComparison.OrdinalIgnoreCase)))
            {
                normalized = "#B45309";
            }

            cat.ColorHex = normalized ?? "#16A34A";
            cat.Extensions = cat.Extensions
                .Select(FileCategory.NormalizeExtension)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static List<FileCategory> CreateDefaultCategories() =>
    [
        new(
            "images",
            "Imagens",
            "#16A34A",
            [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico", ".tiff", ".heic", ".psd"],
            isBuiltIn: true),
        new(
            "audio",
            "Áudio",
            "#8B5CF6",
            [".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"],
            isBuiltIn: true),
        new(
            "video",
            "Vídeos",
            "#DC2626",
            [".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv"],
            isBuiltIn: true),
        new(
            "documents",
            "Documentos",
            "#0078D4",
            [".docx", ".doc", ".odt", ".rtf", ".pdf", ".txt", ".md"],
            isBuiltIn: true),
        new(
            "spreadsheets",
            "Planilhas",
            "#0D9488",
            [".xlsx", ".xls", ".csv", ".ods"],
            isBuiltIn: true),
        new(
            "presentations",
            "Apresentações",
            "#EA580C",
            [".pptx", ".ppt", ".odp", ".key"],
            isBuiltIn: true),
        new(
            "code",
            "Código",
            "#6366F1",
            [".cs", ".py", ".js", ".ts", ".json", ".xml", ".html", ".css", ".cpp", ".java", ".sql", ".sh", ".bat", ".ps1"],
            isBuiltIn: true),
        new(
            "archives",
            "Compactados",
            "#B45309",
            [".zip", ".rar", ".7z", ".tar", ".gz"],
            isBuiltIn: true)
    ];
}
