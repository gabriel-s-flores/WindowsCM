// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Popup;

// Semantic color mapping for clipboard item kinds (Windows 11 Fluent palette).
// Provides subtle, distinct accent and background colors for both Light and Dark modes,
// with full support for user-customized colors.
public static class ItemTypeTheme
{
    public static string GetKindAccentHex(
        ItemKind kind,
        bool isLight,
        string? content = null,
        ItemColorSettings? customColors = null,
        FileCategorySettings? fileCategories = null)
    {
        if (kind == ItemKind.Color)
        {
            var parsed = TryParseColorHex(content);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        if ((kind == ItemKind.File || kind == ItemKind.Files) && fileCategories != null && !string.IsNullOrWhiteSpace(content))
        {
            var firstPath = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var cat = fileCategories.ResolveCategory(firstPath);
            if (cat != null && !string.IsNullOrWhiteSpace(cat.ColorHex))
            {
                return cat.ColorHex;
            }
        }

        var custom = customColors?.GetCustomColor(kind);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        return kind switch
        {
            ItemKind.Link => isLight ? "#0067B8" : "#4CC2FF",
            ItemKind.Code => isLight ? "#6366F1" : "#A78BFA",
            ItemKind.File or ItemKind.Files => isLight ? "#D97706" : "#FBBF24",
            ItemKind.Image => isLight ? "#16A34A" : "#4ADE80",
            ItemKind.Character => isLight ? "#E11D48" : "#FB7185",
            ItemKind.Color => isLight ? "#C026D3" : "#E879F9",
            ItemKind.Text => isLight ? "#475569" : "#94A3B8",
            _ => isLight ? "#64748B" : "#94A3B8"
        };
    }

    public static string GetKindBackgroundHex(
        ItemKind kind,
        bool isLight,
        string? content = null,
        ItemColorSettings? customColors = null,
        FileCategorySettings? fileCategories = null)
    {
        // 12% alpha in Light mode, 16% alpha in Dark mode for subtle pill/badge backgrounds
        var accent = GetKindAccentHex(kind, isLight, content, customColors, fileCategories);
        if (accent.StartsWith('#') && accent.Length == 7)
        {
            var alpha = isLight ? "1F" : "26"; // ~12% in light, ~15% in dark
            return $"#{alpha}{accent[1..]}";
        }
        return isLight ? "#1F0078D4" : "#264CC2FF";
    }

    private static string? TryParseColorHex(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var parsed = ColorParser.TryParse(content.Trim());
        if (parsed != null && (parsed.Space == ColorSpace.Hex || parsed.Space == ColorSpace.Rgb))
        {
            return $"#{(int)parsed.C1:X2}{(int)parsed.C2:X2}{(int)parsed.C3:X2}";
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith('#') && (trimmed.Length == 7 || trimmed.Length == 4))
        {
            return trimmed;
        }

        return null;
    }
}
