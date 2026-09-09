// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Highlight seam (Copyous code prefs parity: syntax-highlighting default
// true; fallback without hljs = escaped monospace TextBlock). The UI layer
// owns the read-only AvalonEdit control; Core owns the decision —
// highlighted only when detection produced a known language AND the
// highlighter is available AND the setting is on. Everything else is
// plain text, so cards never break.
public sealed class CodeHighlightTests
{
    [Fact]
    public void Plan_KnownLanguage_ReturnsHighlightedWithDefinition()
    {
        var plan = CodeHighlightPlanner.Plan("csharp", HighlightingAvailable: true, SyntaxHighlightingEnabled: true);

        Assert.Equal(CodeHighlightMode.Highlighted, plan.Mode);
        Assert.Equal("C#", plan.DefinitionName);
    }

    [Fact]
    public void Plan_LanguageId_IsCaseInsensitive()
    {
        var plan = CodeHighlightPlanner.Plan("  Python ", true, true);

        Assert.Equal(CodeHighlightMode.Highlighted, plan.Mode);
        Assert.Equal("Python", plan.DefinitionName);
    }

    [Fact]
    public void Plan_UnknownLanguage_FallsBackToPlainText()
    {
        var plan = CodeHighlightPlanner.Plan("zephir-unknown", true, true);

        Assert.Equal(CodeHighlightMode.PlainText, plan.Mode);
        Assert.Null(plan.DefinitionName);
    }

    [Fact]
    public void Plan_NullLanguage_FallsBackToPlainText()
    {
        Assert.Equal(CodeHighlightMode.PlainText, CodeHighlightPlanner.Plan(null, true, true).Mode);
        Assert.Equal(CodeHighlightMode.PlainText, CodeHighlightPlanner.Plan("  ", true, true).Mode);
    }

    [Fact]
    public void Plan_HighlighterUnavailable_FallsBackToPlainText()
    {
        var plan = CodeHighlightPlanner.Plan("csharp", HighlightingAvailable: false, SyntaxHighlightingEnabled: true);

        Assert.Equal(CodeHighlightMode.PlainText, plan.Mode);
    }

    [Fact]
    public void Plan_SettingDisabled_FallsBackToPlainText()
    {
        var plan = CodeHighlightPlanner.Plan("csharp", HighlightingAvailable: true, SyntaxHighlightingEnabled: false);

        Assert.Equal(CodeHighlightMode.PlainText, plan.Mode);
    }

    [Fact]
    public void LanguageMap_CoversCommonHljsIds()
    {
        foreach (var id in new[] { "python", "javascript", "typescript", "csharp", "java", "go", "rust", "sql", "json", "xml", "shell", "powershell", "yaml", "markdown" })
        {
            Assert.True(CodeLanguageMap.TryGetDefinition(id, out _), $"missing map for {id}");
        }
    }
}
