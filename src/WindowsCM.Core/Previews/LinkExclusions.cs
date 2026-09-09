// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace WindowsCM.Core.Previews;

// Link exclusion regexes from the per-type prefs (Copyous link `exclusion`
// regex[] parity, 01 §5). Same untrusted-pattern rules as ActionMatcher:
// CultureInvariant, 2s timeout, a bad pattern or a timeout matches nothing
// for that pattern (fail open: the link still previews).
public static class LinkExclusions
{
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    public static bool IsExcluded(string? url, IEnumerable<string>? patterns)
    {
        if (string.IsNullOrEmpty(url) || patterns is null)
        {
            return false;
        }
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }
            try
            {
                if (new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout)
                    .IsMatch(url))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Invalid pattern: no-match for this pattern (fail open).
            }
            catch (RegexMatchTimeoutException)
            {
                // Anti-ReDoS: a timed-out pattern excludes nothing.
            }
        }
        return false;
    }
}
