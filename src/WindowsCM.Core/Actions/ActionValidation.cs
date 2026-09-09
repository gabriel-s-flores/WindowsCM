// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Actions;

// CRUD guards for the prefs UI (Copyous `actionDialog` parity: command
// demands a name and a command, color demands a name, QR demands a name).
// Null means valid; otherwise a human-readable reason (shown, never thrown).
public static class ActionValidation
{
    public static string? Validate(ActionNode node) => node switch
    {
        CommandAction command =>
            string.IsNullOrWhiteSpace(command.Name) ? "The action needs a name."
            : string.IsNullOrWhiteSpace(command.Command) ? "The command action needs a command."
            : null,
        ColorAction color =>
            string.IsNullOrWhiteSpace(color.Name) ? "The action needs a name." : null,
        QrCodeAction qr =>
            string.IsNullOrWhiteSpace(qr.Name) ? "The action needs a name." : null,
        UnknownAction unknown =>
            string.IsNullOrWhiteSpace(unknown.Name) ? "The action needs a name." : null,
        ActionSubmenu submenu =>
            string.IsNullOrWhiteSpace(submenu.Name) ? "The submenu needs a name." : null,
        _ => "Unknown action shape.",
    };

    // Fresh ids for user-created actions (Copyous `uuid_string_random` port).
    public static string NewId() => Guid.NewGuid().ToString();
}
