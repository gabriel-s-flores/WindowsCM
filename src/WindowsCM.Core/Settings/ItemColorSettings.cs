// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Settings;

// Settings for user-customized semantic accent colors per item kind.
// Empty string means using the default Windows 11 Fluent theme colors.
public sealed partial class ItemColorSettings
{
    public string Link { get; set; } = "";
    public string Code { get; set; } = "";
    public string File { get; set; } = "";
    public string Image { get; set; } = "";
    public string Character { get; set; } = "";
    public string Color { get; set; } = "";
    public string Text { get; set; } = "";

    public string? GetCustomColor(ItemKind kind)
    {
        var raw = kind switch
        {
            ItemKind.Link => Link,
            ItemKind.Code => Code,
            ItemKind.File or ItemKind.Files => File,
            ItemKind.Image => Image,
            ItemKind.Character => Character,
            ItemKind.Color => Color,
            ItemKind.Text => Text,
            _ => null
        };

        return NormalizeHex(raw);
    }

    public void SetCustomColor(ItemKind kind, string? hex)
    {
        var normalized = NormalizeHex(hex) ?? "";
        switch (kind)
        {
            case ItemKind.Link: Link = normalized; break;
            case ItemKind.Code: Code = normalized; break;
            case ItemKind.File:
            case ItemKind.Files: File = normalized; break;
            case ItemKind.Image: Image = normalized; break;
            case ItemKind.Character: Character = normalized; break;
            case ItemKind.Color: Color = normalized; break;
            case ItemKind.Text: Text = normalized; break;
        }
    }

    public bool IsCustomized(ItemKind kind) => GetCustomColor(kind) is not null;

    public void Reset(ItemKind kind) => SetCustomColor(kind, null);

    public void ResetAll()
    {
        Link = "";
        Code = "";
        File = "";
        Image = "";
        Character = "";
        Color = "";
        Text = "";
    }

    public void Clamp()
    {
        Link = NormalizeHex(Link) ?? "";
        Code = NormalizeHex(Code) ?? "";
        File = NormalizeHex(File) ?? "";
        Image = NormalizeHex(Image) ?? "";
        Character = NormalizeHex(Character) ?? "";
        Color = NormalizeHex(Color) ?? "";
        Text = NormalizeHex(Text) ?? "";
    }

    public static string? NormalizeHex(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = "#" + trimmed;
        }

        if (trimmed.Length == 4 && HexRegex().IsMatch(trimmed))
        {
            return $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}".ToUpperInvariant();
        }

        if (trimmed.Length == 7 && HexRegex().IsMatch(trimmed))
        {
            return trimmed.ToUpperInvariant();
        }

        if (trimmed.Length == 9 && HexRegex().IsMatch(trimmed))
        {
            return trimmed.ToUpperInvariant();
        }

        return null;
    }

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexRegex();
}
