// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;

namespace WindowsCM.Core.Classification;

// Grapheme counting for Character detection (Copyous uses Intl.Segmenter;
// StringInfo text elements are the ICU-backed .NET equivalent: surrogate
// pairs, combining marks, flags and ZWJ sequences each count as one,
// verified by test).
public static class GraphemeCounter
{
    public static int Count(string text) => new StringInfo(text).LengthInTextElements;

    public static bool HasMoreThan(string text, int max)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        for (var seen = 0; seen <= max; seen++)
        {
            if (!enumerator.MoveNext())
            {
                return false;
            }
        }
        return true;
    }
}
