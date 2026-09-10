// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// Single-line text protocol v1 (research 05 §10): lowercase words, one per
// line, case-insensitive on read. Line framing avoids JSON half-reads —
// commands carry no payload. Unknown input answers "unknown", never throws.
public static class IpcProtocol
{
    public const string Ok = "ok";
    public const string Unknown = "unknown";

    public static string Format(IpcCommand command) => command switch
    {
        IpcCommand.Toggle => "toggle",
        IpcCommand.Show => "show",
        IpcCommand.Hide => "hide",
        IpcCommand.Clear => "clear",
        IpcCommand.ClearAll => "clear-all",
        IpcCommand.Ping => "ping",
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    public static bool TryParse(string? line, out IpcCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        var normalized = line.Trim().ToLowerInvariant();
        // Accept the underscore alias for shells that mangle hyphens.
        normalized = normalized.Replace('_', '-');
        switch (normalized)
        {
            case "toggle":
                command = IpcCommand.Toggle;
                return true;
            case "show":
                command = IpcCommand.Show;
                return true;
            case "hide":
                command = IpcCommand.Hide;
                return true;
            case "clear":
                command = IpcCommand.Clear;
                return true;
            case "clear-all":
                command = IpcCommand.ClearAll;
                return true;
            case "ping":
                command = IpcCommand.Ping;
                return true;
            default:
                return false;
        }
    }
}
