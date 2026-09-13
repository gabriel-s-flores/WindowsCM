// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Previews;

public enum CodeSyntaxTokenKind
{
    PlainText,
    Keyword,
    Type,
    String,
    Comment,
    Number,
    Operator
}

public sealed record CodeSyntaxSpan(string Text, CodeSyntaxTokenKind Kind);

public static class CodeSyntaxTokenizer
{
    private static readonly HashSet<string> UniversalKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // General OO & procedural
        "abstract", "as", "async", "await", "base", "break", "case", "catch", "class",
        "const", "continue", "debugger", "default", "delegate", "delete", "do", "dynamic",
        "elif", "else", "enum", "event", "export", "extends", "extern", "false", "finally",
        "fixed", "fn", "for", "foreach", "from", "func", "function", "get", "global",
        "goto", "if", "implements", "implicit", "import", "in", "inline", "instanceof",
        "interface", "internal", "is", "lambda", "let", "lock", "loop", "match", "module",
        "mut", "namespace", "new", "nil", "none", "null", "of", "operator", "out",
        "override", "package", "params", "pass", "private", "protected", "pub", "public",
        "raise", "readonly", "record", "ref", "return", "sealed", "self", "set", "sizeof",
        "stackalloc", "static", "struct", "super", "switch", "this", "throw", "throws",
        "trait", "true", "try", "type", "typeof", "undefined", "unsafe", "use", "using",
        "val", "var", "virtual", "void", "volatile", "where", "while", "with", "yield"
    };

    private static readonly HashSet<string> UniversalTypes = new(StringComparer.Ordinal)
    {
        // C# / Java / C++
        "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
        "float", "double", "decimal", "bool", "boolean", "char", "string", "object",
        "Task", "List", "Dictionary", "IEnumerable", "IList", "Action", "Func",
        // TypeScript / JS
        "number", "any", "unknown", "never", "symbol", "bigint", "Array", "Promise", "Record",
        // Rust / Go
        "i8", "i16", "i32", "i64", "i128", "isize",
        "u8", "u16", "u32", "u64", "u128", "usize",
        "f32", "f64", "str", "String", "bool",
        "int8", "int16", "int32", "int64", "uint8", "uint16", "uint32", "uint64", "float32", "float64"
    };

    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET",
        "DELETE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "CROSS",
        "ON", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET", "CREATE",
        "TABLE", "DROP", "ALTER", "ADD", "COLUMN", "INDEX", "VIEW", "DATABASE",
        "UNION", "ALL", "DISTINCT", "AS", "AND", "OR", "NOT", "NULL", "IS",
        "LIKE", "IN", "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END"
    };

    public static IReadOnlyList<CodeSyntaxSpan> Tokenize(string? rawCode, int maxLines = 8)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return Array.Empty<CodeSyntaxSpan>();
        }

        var lines = rawCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var isTruncated = lines.Length > maxLines;
        var selectedLines = lines.Take(maxLines).ToList();
        var code = string.Join("\n", selectedLines);
        if (isTruncated)
        {
            code += "\n...";
        }

        var spans = new List<CodeSyntaxSpan>();
        var len = code.Length;
        var i = 0;

        while (i < len)
        {
            var ch = code[i];

            // 1. Comments: // or /* or # or --
            if (ch == '/' && i + 1 < len && code[i + 1] == '/')
            {
                var start = i;
                i += 2;
                while (i < len && code[i] != '\n')
                {
                    i++;
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Comment));
                continue;
            }
            if (ch == '/' && i + 1 < len && code[i + 1] == '*')
            {
                var start = i;
                i += 2;
                while (i < len && !(code[i] == '*' && i + 1 < len && code[i + 1] == '/'))
                {
                    i++;
                }
                if (i < len) i += 2;
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Comment));
                continue;
            }
            if (ch == '#' && (i == 0 || char.IsWhiteSpace(code[i - 1])))
            {
                var start = i;
                i++;
                while (i < len && code[i] != '\n')
                {
                    i++;
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Comment));
                continue;
            }
            if (ch == '-' && i + 1 < len && code[i + 1] == '-')
            {
                var start = i;
                i += 2;
                while (i < len && code[i] != '\n')
                {
                    i++;
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Comment));
                continue;
            }

            // 2. Strings: ", ', `
            if (ch is '"' or '\'' or '`')
            {
                var quote = ch;
                var start = i;
                i++;
                while (i < len && code[i] != quote && code[i] != '\n')
                {
                    if (code[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                if (i < len && code[i] == quote)
                {
                    i++;
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.String));
                continue;
            }

            // 3. Numbers: 0x... or digits
            if (char.IsAsciiDigit(ch) && (i == 0 || !char.IsAsciiLetterOrDigit(code[i - 1])))
            {
                var start = i;
                if (ch == '0' && i + 1 < len && (code[i + 1] is 'x' or 'X'))
                {
                    i += 2;
                    while (i < len && char.IsAsciiHexDigit(code[i])) i++;
                }
                else
                {
                    while (i < len && (char.IsAsciiDigit(code[i]) || code[i] == '.' || code[i] == '_'))
                    {
                        i++;
                    }
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Number));
                continue;
            }

            // 4. Identifiers / Keywords / Types
            if (char.IsAsciiLetter(ch) || ch == '_' || ch == '$' || ch == '@')
            {
                var start = i;
                i++;
                while (i < len && (char.IsAsciiLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }
                var word = code[start..i];

                if (UniversalKeywords.Contains(word) || SqlKeywords.Contains(word))
                {
                    spans.Add(new CodeSyntaxSpan(word, CodeSyntaxTokenKind.Keyword));
                }
                else if (UniversalTypes.Contains(word))
                {
                    spans.Add(new CodeSyntaxSpan(word, CodeSyntaxTokenKind.Type));
                }
                else
                {
                    spans.Add(new CodeSyntaxSpan(word, CodeSyntaxTokenKind.PlainText));
                }
                continue;
            }

            // 5. Operators
            if (ch is '=' or '!' or '<' or '>' or '+' or '-' or '*' or '/' or '&' or '|' or '^' or '~' or '?' or ':')
            {
                var start = i;
                i++;
                if (i < len && (code[i] is '=' or '>' or '<' or '+' or '-' or '&' or '|' or '?' or ':'))
                {
                    i++;
                }
                spans.Add(new CodeSyntaxSpan(code[start..i], CodeSyntaxTokenKind.Operator));
                continue;
            }

            // 6. Whitespace and generic characters
            var plainStart = i;
            while (i < len && !char.IsAsciiLetter(code[i]) && !char.IsAsciiDigit(code[i])
                   && code[i] is not ('_' or '$' or '@' or '"' or '\'' or '`' or '/' or '#' or '-' or '=' or '!' or '<' or '>' or '+' or '*' or '&' or '|' or '^' or '~' or '?' or ':'))
            {
                i++;
            }
            if (i > plainStart)
            {
                spans.Add(new CodeSyntaxSpan(code[plainStart..i], CodeSyntaxTokenKind.PlainText));
            }
        }

        return spans;
    }

    public static string? DetectLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (Regex.IsMatch(code, @"\b(SELECT\s+.*\s+FROM|INSERT\s+INTO|CREATE\s+TABLE|UPDATE\s+.*\s+SET)\b", RegexOptions.IgnoreCase))
        {
            return "SQL";
        }
        if (code.Contains("def ") || code.Contains("elif ") || code.Contains("import ") && code.Contains("from "))
        {
            return "Python";
        }
        if (code.Contains("namespace ") || code.Contains("using System") || code.Contains("public class ") || code.Contains("Console.WriteLine") || code.Contains("Task<"))
        {
            return "C#";
        }
        if (code.Contains("const ") || code.Contains("let ") || code.Contains("function ") || code.Contains("console.log") || code.Contains("document."))
        {
            return "JavaScript";
        }
        if (code.Contains("fn ") || code.Contains("impl ") || code.Contains("let mut ") || code.Contains("println!"))
        {
            return "Rust";
        }
        if (code.Contains("func ") || code.Contains("package ") && code.Contains("import ("))
        {
            return "Go";
        }
        if (code.Contains("<html") || code.Contains("<!DOCTYPE html") || code.Contains("<div") || code.Contains("</div>"))
        {
            return "HTML";
        }

        return null;
    }
}
