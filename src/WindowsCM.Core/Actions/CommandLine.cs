// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace WindowsCM.Core.Actions;

// Placeholder expansion for command actions (research 05 §2.2). %1..%9 is
// the canonical Windows spelling (cmd style); $1..$9 is accepted as an
// alias so Linux-ported commands (`nautilus -s $1`) keep working instead
// of failing silently. %1 is the first capture group — the full match
// (group 0) is never addressable, matching `sh -c <cmd> _ <grupos...>`
// where $0 is the throwaway "_". Missing groups expand to empty.
public static class CommandLine
{
    // cmd.exe invocation prefix for a substituted command.
    public const string ShellPrefix = "/c ";

    public static string Substitute(string command, IReadOnlyList<string> groups)
    {
        if (string.IsNullOrEmpty(command) || groups.Count == 0)
        {
            return command;
        }
        var result = new StringBuilder(command.Length + groups.Count * 8);
        for (var i = 0; i < command.Length; i++)
        {
            var marker = command[i];
            if ((marker == '%' || marker == '$') && i + 1 < command.Length)
            {
                var next = command[i + 1];
                if (next is >= '1' and <= '9')
                {
                    var index = next - '1';
                    if (index < groups.Count)
                    {
                        result.Append(groups[index]);
                    }
                    i++;
                    continue;
                }
            }
            result.Append(marker);
        }
        return result.ToString();
    }

    // Full Arguments for cmd.exe: the substituted command plus the groups
    // as trailing argv (Copyous parity: groups travel both as $N and as
    // argv). Trailing args with whitespace are quoted; substituted values
    // are verbatim, so custom commands must quote their own %1.
    public static string BuildArguments(string command, IReadOnlyList<string> groups)
    {
        var substituted = Substitute(command, groups);
        if (groups.Count == 0)
        {
            return ShellPrefix + substituted;
        }
        var trailing = string.Join(" ", groups.Select(Quote));
        return ShellPrefix + substituted + " " + trailing;
    }

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) ? "\"" + value + "\"" : value;
}
