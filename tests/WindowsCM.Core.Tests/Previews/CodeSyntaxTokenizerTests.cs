// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

public sealed class CodeSyntaxTokenizerTests
{
    [Fact]
    public void Tokenize_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(CodeSyntaxTokenizer.Tokenize(null));
        Assert.Empty(CodeSyntaxTokenizer.Tokenize(""));
        Assert.Empty(CodeSyntaxTokenizer.Tokenize("   \n\t  "));
    }

    [Fact]
    public void Tokenize_CSharpClassSnippet_IdentifiesKeywordsAndTypes()
    {
        const string code = "public async Task<int> CalculateAsync(string name)";

        var tokens = CodeSyntaxTokenizer.Tokenize(code);

        Assert.NotEmpty(tokens);
        // "public", "async" -> Keyword
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Keyword && t.Text == "public");
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Keyword && t.Text == "async");
        // "int", "string" -> Type
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Type && t.Text == "int");
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Type && t.Text == "string");
    }

    [Fact]
    public void Tokenize_JavaScriptSnippet_IdentifiesStringsAndFunctions()
    {
        const string code = "const message = 'Hello, world!';\nreturn message;";

        var tokens = CodeSyntaxTokenizer.Tokenize(code);

        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Keyword && t.Text == "const");
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Keyword && t.Text == "return");
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.String && t.Text == "'Hello, world!'");
    }

    [Fact]
    public void Tokenize_Comments_IdentifiedCorrectly()
    {
        const string code = "// Single line comment\nlet x = 10; /* multi line */";

        var tokens = CodeSyntaxTokenizer.Tokenize(code);

        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Comment && t.Text.StartsWith("//"));
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Comment && t.Text == "/* multi line */");
        Assert.Contains(tokens, t => t.Kind == CodeSyntaxTokenKind.Number && t.Text == "10");
    }

    [Fact]
    public void Tokenize_MaxLines_TruncatesCleanly()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"int x{i} = {i};"));

        var tokens = CodeSyntaxTokenizer.Tokenize(lines, maxLines: 5);

        var reconstructed = string.Concat(tokens.Select(t => t.Text));
        var lineCount = reconstructed.Split('\n').Length;
        Assert.True(lineCount <= 6); // 5 lines + optional "..."
    }

    [Theory]
    [InlineData("def process(items):\n    return [x for x in items]", "Python")]
    [InlineData("public class Program {\n    static void Main() {}\n}", "C#")]
    [InlineData("function fetchData() {\n    const res = await fetch();\n}", "JavaScript")]
    [InlineData("SELECT id, name FROM users WHERE active = 1;", "SQL")]
    public void DetectLanguage_IdentifiesCommonLanguages(string code, string expectedLang)
    {
        var lang = CodeSyntaxTokenizer.DetectLanguage(code);
        Assert.Equal(expectedLang, lang);
    }
}
