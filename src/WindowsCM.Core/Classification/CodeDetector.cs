// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Classification;

// Code detection. Copyous asks highlight.js `highlightAuto` on a 10k slice
// and accepts the language when `relevance / max(1, len/100) >= 3`. Shipping
// highlight.js is out of scope for v1 (the AvalonEdit language-map spike in
// the spec will replace this), so this is a documented heuristic with the
// same shape: a relevance score over the same 10k slice tested against the
// same density threshold.
//
// Score: distinctive keywords x2, code punctuation x1, indented or
// colon-terminated lines x1. The gate keeps prose out: bare English words
// overlap keyword lists (`for`, `delete the file (see below)`), so keywords
// only count beside hard punctuation (`;{}[]()#@$\\`, lone `=`, two-char
// operators, comment brackets). `*`, `:`, `,`, `.` are deliberately not
// hard signals — markdown and lists use them constantly.
public static class CodeDetector
{
    public const int MaxSliceLength = 10000;
    public const double DensityThreshold = 3.0;

    private static readonly Regex KeywordRegex = new(
        @"\b(function|return|var|let|const|class|struct|interface|enum|public|private|protected|"
        + @"static|void|int|long|short|byte|char|string|bool|float|double|decimal|typeof|instanceof|"
        + @"import|from|export|using|namespace|package|include|define|typedef|template|typename|"
        + @"virtual|override|abstract|sealed|async|await|try|catch|finally|throw|extends|implements|"
        + @"def|lambda|where|join|values|pass|raise|yield|switch|foreach|while|continue|break|"
        + @"default|sizeof|readonly|volatile|explicit|operator|params|record|init|nameof|checked|"
        + @"unchecked|fixed|stackalloc|goto|lock|event|delegate|elif|echo)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MultiOperatorRegex = new(
        @"=>|==|!=|<=|>=|&&|\|\||//|/\*|\*/|->|::|<\?|\?>|</",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // A lone `=`: assignment/SQL comparison, but not part of ==, =>, !=, <=, >=.
    private static readonly Regex LoneEqualsRegex = new(
        @"(?<![=!<>])=(?![=>])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsCode(string? trimmed)
    {
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }
        var slice = trimmed.Length > MaxSliceLength
            ? trimmed.Substring(0, MaxSliceLength)
            : trimmed;
        var (keywords, hard, soft) = Signals(slice);
        if (hard < 2 || (keywords == 0 && hard < 3))
        {
            return false;
        }
        var n = Math.Max(1, slice.Length / 100);
        return (double)Relevance(keywords, hard, soft) / n >= DensityThreshold;
    }

    // Score shape for diagnostics; IsCode is the behavior seam.
    private static int Relevance(string slice)
    {
        var (keywords, hard, soft) = Signals(slice);
        return Relevance(keywords, hard, soft);
    }

    private static int Relevance(int keywords, int hard, int soft) =>
        2 * keywords + hard + soft;

    private static (int Keywords, int Hard, int Soft) Signals(string slice)
    {
        var keywords = KeywordRegex.Matches(slice).Count;
        var hard = slice.Count(c => ";{}[]()#@$\\".Contains(c))
            + MultiOperatorRegex.Matches(slice).Count
            + LoneEqualsRegex.Matches(slice).Count;
        var soft = 0;
        foreach (var rawLine in slice.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line.Trim().Length == 0)
            {
                continue;
            }
            if (line[0] is ' ' or '\t')
            {
                soft++;
            }
            if (line.EndsWith(':'))
            {
                soft++;
            }
        }
        return (keywords, hard, soft);
    }
}
