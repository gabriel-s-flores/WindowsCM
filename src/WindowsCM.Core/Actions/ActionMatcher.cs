// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

// Applicability of an action to an item (Copyous `testAction`/`matchAction`
// parity). Types filter first — null/empty means all eight kinds — then the
// pattern runs with a 2s match timeout (research 05 §1.3: patterns come from
// an editable file, so untrusted-pattern rules apply). A bad pattern or a
// timeout is no-match, never an exception.
//
// Engine semantics differ from JS RegExp by documentation, not by flag:
// .NET matches canonically (Unicode: \w covers far more than [A-Za-z0-9_],
// same for \b and \d). Ports like ^(?!rgb) behave identically; character
// classes may match more than they did on GNOME.
public static class ActionMatcher
{
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    // Copyous testAction: type subset, then regex test.
    public static bool Test(ItemKind kind, string content, ClipboardAction action)
    {
        if (action.Types is { Count: > 0 } && !action.Types.Contains(kind))
        {
            return false;
        }
        if (string.IsNullOrEmpty(action.Pattern))
        {
            return true;
        }
        try
        {
            return new Regex(
                action.Pattern,
                RegexOptions.CultureInvariant,
                MatchTimeout).IsMatch(content);
        }
        catch (ArgumentException)
        {
            // Invalid pattern: no-match (Copyous :91-93 parity).
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // Anti-ReDoS: a timed-out pattern matches nothing.
            return false;
        }
    }

    // Copyous matchAction: the match array with Groups[0] as the full
    // match, unmatched groups as null. Null means no-match. A missing
    // pattern matches everything as [content].
    public static IReadOnlyList<string?>? Match(ItemKind kind, string content, ClipboardAction action)
    {
        if (action.Types is { Count: > 0 } && !action.Types.Contains(kind))
        {
            return null;
        }
        if (string.IsNullOrEmpty(action.Pattern))
        {
            return [content];
        }
        try
        {
            var match = new Regex(
                action.Pattern,
                RegexOptions.CultureInvariant,
                MatchTimeout).Match(content);
            if (!match.Success)
            {
                return null;
            }
            return match.Groups.Cast<Group>()
                .Select(g => g.Success ? g.Value : null)
                .ToList();
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }
}
