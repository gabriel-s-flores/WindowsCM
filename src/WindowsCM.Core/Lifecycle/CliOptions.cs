// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// CLI mirror of the pipe protocol (research 05 §10): --toggle | --show |
// --hide | --clear | --clear-all plus the --hidden start flag (Run-key
// autostart launches "exe" --hidden). --clear keeps pins+tags, --clear-all
// wipes everything. Parsing never throws: unknown tokens are ignored for
// forward-compat, the first command wins, --hidden composes with a command.
public sealed record CliOptions(IpcCommand? Command, bool StartHidden, bool ShowHelp)
{
    public string? FormatCommand() => Command is null ? null : IpcProtocol.Format(Command.Value);

    // Second launches with no command toggle the popup (single-instance UX);
    // the primary with no command just starts normally. A bare --hidden
    // second launch (autostart) forwards nothing: null means "exit quietly",
    // never pop the window on login.
    public string? FormatForForward() =>
        Command is null && StartHidden ? null : FormatCommand() ?? IpcProtocol.Format(IpcCommand.Toggle);

    public static CliOptions Parse(string[] args)
    {
        IpcCommand? command = null;
        var hidden = false;
        var help = false;
        foreach (var raw in args)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }
            var token = raw.Trim();
            // Skip the exe path (GetCommandLineArgs[0] never starts with -//).
            if (token.Length == 0 || (token[0] != '-' && token[0] != '/'))
            {
                continue;
            }
            var name = token.TrimStart('-', '/').ToLowerInvariant().Replace('_', '-');
            switch (name)
            {
                case "hidden":
                    hidden = true;
                    break;
                case "help":
                case "h":
                case "?":
                    help = true;
                    break;
                case "toggle":
                    command ??= IpcCommand.Toggle;
                    break;
                case "show":
                    command ??= IpcCommand.Show;
                    break;
                case "hide":
                    command ??= IpcCommand.Hide;
                    break;
                case "clear":
                    command ??= IpcCommand.Clear;
                    break;
                case "clear-all":
                    command ??= IpcCommand.ClearAll;
                    break;
                case "ping":
                    command ??= IpcCommand.Ping;
                    break;
                default:
                    break;
            }
        }
        return new CliOptions(command, hidden, help);
    }
}
