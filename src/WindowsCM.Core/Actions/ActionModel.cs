// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

// Copyous `ActionOutput` parity: what happens to a command/color result.
// QR actions are always Ignore (the dialog shows the payload; nothing is
// injected into any app).
public enum ActionOutput
{
    Ignore,
    Copy,
    Paste,
}

// One row of actions.json: either a runnable action or a named submenu.
// Submenus nest (Copyous `ActionSubmenu`); ids live only on actions.
public abstract record ActionNode;

// Shared shape of every runnable action (Copyous `Action` parity):
// Pattern is a .NET regex (null/empty = always), Types null/empty = all
// eight item kinds, Shortcut holds WPF gesture strings ("Ctrl+Q") parsed
// by the UI layer — Core never interprets them except to ship defaults.
public abstract record ClipboardAction(
    string Kind,
    string Id,
    string Name,
    string? Pattern,
    List<ItemKind>? Types,
    ActionOutput Output,
    List<string>? Shortcut) : ActionNode;

// A shell command run with the item content on stdin (Copyous
// `CommandAction` parity). Groups from Pattern fill %1..%9 (and the $1..$9
// alias for Linux-ported commands) and are appended as trailing argv.
public sealed record CommandAction(
    string Id,
    string Name,
    string Command,
    string? Pattern,
    List<ItemKind>? Types,
    ActionOutput Output,
    List<string>? Shortcut)
    : ClipboardAction("command", Id, Name, Pattern, Types, Output, Shortcut);

// A color conversion reusing the ported parser (Copyous `ColorAction`
// parity). Types is always [Color]; Output is Copy or Paste.
public sealed record ColorAction(
    string Id,
    string Name,
    ColorSpace Space,
    string? Pattern,
    List<ItemKind>? Types,
    ActionOutput Output,
    List<string>? Shortcut)
    : ClipboardAction("color", Id, Name, Pattern, Types, Output, Shortcut);

// A QR payload request (Copyous `QrCodeAction` parity). Output is always
// Ignore; bitmap rendering (QRCoder) belongs to the UI dialog, Core only
// decides eligibility and hands over the content.
public sealed record QrCodeAction(
    string Id,
    string Name,
    string? Pattern,
    List<ItemKind>? Types,
    List<string>? Shortcut)
    : ClipboardAction("qrcode", Id, Name, Pattern, Types, ActionOutput.Ignore, Shortcut);

// Forward-compat keeper: an action whose kind this build does not know.
// Preserved verbatim across load/save so newer configs are never mangled;
// the executor refuses to run it with diagnostics.
public sealed record UnknownAction(
    string Kind,
    string Id,
    string Name,
    string? Pattern,
    List<ItemKind>? Types,
    ActionOutput Output,
    List<string>? Shortcut)
    : ClipboardAction(Kind, Id, Name, Pattern, Types, Output, Shortcut);

// A named submenu grouping actions (Copyous `ActionSubmenu` parity).
public sealed record ActionSubmenu(
    string Name,
    List<ActionNode> Actions) : ActionNode;

// The whole actions.json document (Copyous `ActionConfig` parity).
// Defaults maps an item kind to the id of its default action; a kind
// without an entry (or with an unknown id) has no default (None in UI).
public sealed record ActionConfig(
    List<ActionNode> Actions,
    Dictionary<ItemKind, string> Defaults);

// Depth-first traversal and default resolution (Copyous `findActionById`,
// `isDefaultAction`, `findDefaultAction` parity). Submenu members resolve
// exactly like top-level actions — the Link default lives inside Open.
public static class ActionCatalog
{
    public static IEnumerable<ClipboardAction> Flatten(ActionConfig config)
    {
        foreach (var node in config.Actions)
        {
            foreach (var action in FlattenNode(node))
            {
                yield return action;
            }
        }
    }

    private static IEnumerable<ClipboardAction> FlattenNode(ActionNode node)
    {
        if (node is ClipboardAction action)
        {
            yield return action;
        }
        else if (node is ActionSubmenu submenu)
        {
            foreach (var child in submenu.Actions)
            {
                foreach (var nested in FlattenNode(child))
                {
                    yield return nested;
                }
            }
        }
    }

    public static ClipboardAction? FindById(ActionConfig config, string id) =>
        Flatten(config).FirstOrDefault(a => a.Id == id);

    public static bool IsDefaultAction(ActionConfig config, ItemKind kind, ClipboardAction action) =>
        config.Defaults.TryGetValue(kind, out var id) && id == action.Id;

    public static ClipboardAction? FindDefaultAction(ActionConfig config, ItemKind kind) =>
        config.Defaults.TryGetValue(kind, out var id) ? FindById(config, id) : null;
}
