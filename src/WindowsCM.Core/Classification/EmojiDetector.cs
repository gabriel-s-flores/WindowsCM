// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;

namespace WindowsCM.Core.Classification;

// Pure domain logic for Unicode emoji detection, grapheme analysis and counting.
// Covers Unicode 15.0+ Emoji blocks: Emoticons, Pictographs, Flags (Regional Indicators),
// ZWJ compounds, skin tone modifiers (Fitzpatrick), variation selectors and keycaps.
public static class EmojiDetector
{
    // Returns true if the given text consists exclusively of one or more emoji graphemes
    // and optional whitespace. If any non-whitespace grapheme is a regular letter, number
    // or standard punctuation, returns false.
    public static bool IsAllEmojis(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var emojiCount = 0;

        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (string.IsNullOrWhiteSpace(element))
            {
                continue;
            }

            if (!IsEmojiGrapheme(element))
            {
                return false;
            }

            emojiCount++;
        }

        return emojiCount > 0;
    }

    // Counts the total number of emoji graphemes in the text.
    public static int CountEmojis(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var count = 0;

        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (!string.IsNullOrWhiteSpace(element) && IsEmojiGrapheme(element))
            {
                count++;
            }
        }

        return count;
    }

    // Checks if a single grapheme cluster is an emoji.
    public static bool IsEmojiGrapheme(string grapheme)
    {
        if (string.IsNullOrWhiteSpace(grapheme))
        {
            return false;
        }

        // Keycap check: e.g. "1️⃣", "#️⃣", "*️⃣" (contains \u20E3 Combining Enclosing Keycap)
        if (grapheme.Contains('\u20E3'))
        {
            var firstRune = grapheme.EnumerateRunes().FirstOrDefault();
            var val = firstRune.Value;
            return (val >= '0' && val <= '9') || val == '#' || val == '*';
        }

        var hasPrimaryEmoji = false;

        foreach (var rune in grapheme.EnumerateRunes())
        {
            var val = rune.Value;

            // Modifiers and joiners
            if (val == 0x200D ||                        // Zero Width Joiner (ZWJ)
                val == 0xFE0E || val == 0xFE0F ||      // Variation Selectors 15 and 16
                (val >= 0x1F3FB && val <= 0x1F3FF) ||  // Fitzpatrick Skin Tone Modifiers
                (val >= 0xE0020 && val <= 0xE007F))    // Tag characters (subdivision flags)
            {
                continue;
            }

            // Primary emoji rune ranges
            if (IsPrimaryEmojiRune(val))
            {
                hasPrimaryEmoji = true;
                continue;
            }

            // If it's a regular letter, number, or punctuation that is NOT an emoji modifier,
            // this grapheme cannot be considered an emoji.
            return false;
        }

        return hasPrimaryEmoji;
    }

    // Tests if a codepoint belongs to the Unicode Emoji codepoint specifications.
    public static bool IsPrimaryEmojiRune(int val)
    {
        return (val >= 0x1F300 && val <= 0x1F5FF) || // Misc Symbols and Pictographs
               (val >= 0x1F600 && val <= 0x1F64F) || // Emoticons
               (val >= 0x1F680 && val <= 0x1F6FF) || // Transport and Map
               (val >= 0x1F700 && val <= 0x1F77F) || // Alchemical Symbols
               (val >= 0x1F780 && val <= 0x1F7FF) || // Geometric Shapes Extended
               (val >= 0x1F800 && val <= 0x1F8FF) || // Supplemental Arrows-C
               (val >= 0x1F900 && val <= 0x1F9FF) || // Supplemental Symbols and Pictographs
               (val >= 0x1FA00 && val <= 0x1FA6F) || // Chess Symbols
               (val >= 0x1FA70 && val <= 0x1FAFF) || // Symbols and Pictographs Extended-A
               (val >= 0x1F1E6 && val <= 0x1F1FF) || // Regional Indicator Symbols (Flags)
               (val >= 0x1F191 && val <= 0x1F19A) || // Squared CL, COOL, FREE, ID, NEW, NG, OK, SOS, UP, VS
               (val >= 0x1F200 && val <= 0x1F251) || // Enclosed Ideographic Supplement
               (val >= 0x2600 && val <= 0x26FF)   || // Misc Symbols (☀️, ☁️, ⚠️, ⚡, ☕, ⚽, ⛄)
               (val >= 0x2700 && val <= 0x27BF)   || // Dingbats (✂️, ✈️, ✉️, ✌️, ✨, ❄️, ❌, ❤️)
               (val >= 0x2300 && val <= 0x23FF)   || // Misc Technical (⌚, ⌛, ⏩, ⏪, ⏰, ⏱, ⏳)
               (val >= 0x2B05 && val <= 0x2B07)   || // Arrows (⬅️, ⬆️, ⬇️)
               val == 0x2B50 || val == 0x2B55     || // Star (⭐), Heavy Large Circle (⭕)
               val == 0x2934 || val == 0x2935     || // Arrow curving up / down
               (val >= 0x2194 && val <= 0x2199)   || // Arrows (↔️, ↕️, ↖️, ↗️, ↘️, ↙️)
               val == 0x21A9 || val == 0x21AA     || // Left / Right arrow with hook
               val == 0x203C || val == 0x2049     || // Double Exclamation (‼️), Interrobang (⁉️)
               val == 0x2122 || val == 0x2139     || // Trade Mark (™️), Information (ℹ️)
               val == 0x3030 || val == 0x303D     || // Wavy Dash (〰️), Part Alternation Mark (〽️)
               val == 0x3297 || val == 0x3299;       // Circled Ideograph (㊗️, ㊙️)
    }
}
