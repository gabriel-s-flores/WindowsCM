// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Previews;

public enum CodeHighlightMode
{
    // Rendered through the highlighter (read-only AvalonEdit in the UI).
    Highlighted,
    // Escaped monospace text (Copyous no-hljs fallback parity).
    PlainText,
}

public sealed record CodeHighlightPlan(CodeHighlightMode Mode, string? DefinitionName);

// hljs language id -> AvalonEdit highlighting definition (research 05 §7:
// AvalonEdit v6.3.1 ships dozens of .xshd definitions; unknown ids fall
// back to plain text). Curated common subset of the 192 hljs ids (research
// 01 §1 HljsLanguages); the full 192-row table is a post-v1 spike — cards
// must never break on an unmapped id, so misses are plain text by design.
// Keys are lowercase hljs ids; lookup trims and lowercases its input.
public static class CodeLanguageMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "C",
        ["cpp"] = "C++",
        ["csharp"] = "C#",
        ["cs"] = "C#",
        ["css"] = "CSS",
        ["diff"] = "Diff",
        ["go"] = "Go",
        ["graphql"] = "GraphQL",
        ["html"] = "HTML",
        ["ini"] = "INI",
        ["java"] = "Java",
        ["javascript"] = "JavaScript",
        ["js"] = "JavaScript",
        ["json"] = "Json",
        ["kotlin"] = "Kotlin",
        ["lua"] = "Lua",
        ["markdown"] = "MarkDown",
        ["md"] = "MarkDown",
        ["perl"] = "Perl",
        ["php"] = "PHP",
        ["plaintext"] = "PlainText",
        ["text"] = "PlainText",
        ["powershell"] = "PowerShell",
        ["ps1"] = "PowerShell",
        ["python"] = "Python",
        ["py"] = "Python",
        ["r"] = "R",
        ["ruby"] = "Ruby",
        ["rb"] = "Ruby",
        ["rust"] = "Rust",
        ["shell"] = "Shell",
        ["bash"] = "Shell",
        ["sh"] = "Shell",
        ["sql"] = "SQL",
        ["swift"] = "Swift",
        ["typescript"] = "TypeScript",
        ["ts"] = "TypeScript",
        ["vbnet"] = "VB",
        ["xml"] = "XML",
        ["yaml"] = "YAML",
        ["yml"] = "YAML",
    };

    public static bool TryGetDefinition(string? languageId, out string? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return false;
        }
        return Map.TryGetValue(languageId.Trim(), out definition);
    }
}

// The highlight decision (pure): highlighted only when detection produced
// a known language, the AvalonEdit definitions are available, and the
// per-type syntax-highlighting setting is on. Every other combination is
// plain text — detection/highlighter unavailable never breaks the card.
// The UI still guards per language: if HighlightingManager has no
// definition for DefinitionName, it renders plain text too.
public static class CodeHighlightPlanner
{
    public static CodeHighlightPlan Plan(
        string? languageId,
        bool HighlightingAvailable,
        bool SyntaxHighlightingEnabled)
    {
        if (!SyntaxHighlightingEnabled || !HighlightingAvailable)
        {
            return new CodeHighlightPlan(CodeHighlightMode.PlainText, null);
        }
        if (CodeLanguageMap.TryGetDefinition(languageId, out var definition))
        {
            return new CodeHighlightPlan(CodeHighlightMode.Highlighted, definition);
        }
        return new CodeHighlightPlan(CodeHighlightMode.PlainText, null);
    }
}
