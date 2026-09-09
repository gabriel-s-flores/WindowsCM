// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

// The Windows port of Copyous `defaultConfig` (actions.ts). Same ids,
// same submenu grouping (Open, Convert), same color guards and per-type
// defaults — only the shell commands are rewritten for Windows:
// $1 becomes %1 (cmd style), xdg-open becomes Start-Process, nautilus
// becomes explorer /select, cut becomes a stdin→stdout stripper.
//
// The four command ids below are reserved built-ins: the executor runs
// them natively (shell-open, Explorer reveal, browser open, path rewrite)
// so Linux-authored configs (xdg-open, nautilus, cut) keep working after
// migration. The command strings stay honest fallbacks — copying one into
// a custom action under a new id runs it through the shell.
public static class BuiltinActions
{
    public const string OpenWithDefault = "open-with-default";
    public const string OpenWithFiles = "open-with-files";
    public const string OpenWithBrowser = "open-with-browser";
    public const string PasteAsPath = "paste-as-path";
    public const string QrCode = "qrcode";

    // The QR chord in WPF spelling (GTK original: <Control>q).
    public const string QrShortcut = "Ctrl+Q";

    public static ActionConfig Default()
    {
        ColorAction Convert(string id, string name, string pattern, ColorSpace space) =>
            new(id, name, space, pattern, [ItemKind.Color], ActionOutput.Paste, []);

        return new ActionConfig(
            [
                new ActionSubmenu("Open",
                [
                    new CommandAction(
                        OpenWithDefault, "Open with Default",
                        "powershell -NoProfile -NonInteractive -Command \"$input | ForEach-Object { $p = $_.Trim(); if ($p) { $u = [Uri]::UnescapeDataString(($p -replace 'file://','').Trim()).Trim('/'); if ($u) { Start-Process -FilePath $u } } }\"",
                        null, [ItemKind.Image, ItemKind.File],
                        ActionOutput.Ignore, []),
                    new CommandAction(
                        OpenWithFiles, "Open with Files",
                        "explorer.exe /select,\"%1\"",
                        "^(.*)", [ItemKind.Image, ItemKind.File, ItemKind.Files],
                        ActionOutput.Ignore, []),
                    new CommandAction(
                        OpenWithBrowser, "Open with Browser",
                        "powershell -NoProfile -NonInteractive -Command \"$input | ForEach-Object { $u = $_.Trim(); if ($u) { Start-Process -FilePath $u } }\"",
                        null, [ItemKind.Link],
                        ActionOutput.Ignore, []),
                ]),
                new CommandAction(
                    PasteAsPath, "Paste as Path",
                    "powershell -NoProfile -NonInteractive -Command \"$input | ForEach-Object { $p = $_.Trim(); if ($p) { [Uri]::UnescapeDataString(($p -replace 'file://','').Trim()).Trim('/') } }\"",
                    null, [ItemKind.Image, ItemKind.File, ItemKind.Files],
                    ActionOutput.Paste, []),
                new ActionSubmenu("Convert",
                [
                    Convert("rgb", "Rgb", "^(?!rgb)", ColorSpace.Rgb),
                    Convert("hex", "Hex", "^(?!#)", ColorSpace.Hex),
                    Convert("hsl", "Hsl", "^(?!hsl)", ColorSpace.Hsl),
                    // hwb, linear-rgb, xyz, lab, lch, oklab stay commented,
                    // exactly like the Copyous source — oklch closes the list.
                    Convert("oklch", "Oklch", "^(?!oklch)", ColorSpace.Oklch),
                ]),
                new QrCodeAction(
                    QrCode, "QR Code", null,
                    [ItemKind.Text, ItemKind.Code, ItemKind.Link, ItemKind.Character, ItemKind.Color],
                    [QrShortcut]),
            ],
            new Dictionary<ItemKind, string>
            {
                [ItemKind.File] = PasteAsPath,
                [ItemKind.Files] = PasteAsPath,
                [ItemKind.Link] = OpenWithBrowser,
            });
    }

    // Restore (Copyous `mergeConfig` parity): built-in actions missing from
    // the user config are merged back in, customs and the user's defaults
    // untouched. Same-named submenus group (user children first).
    public static ActionConfig MergeMissing(ActionConfig user, ActionConfig builtins)
    {
        var ids = new HashSet<string>(ActionCatalog.Flatten(user).Select(a => a.Id));
        var missing = FilterMissing(builtins.Actions, ids).ToList();

        var merged = new List<ActionNode>();
        foreach (var node in user.Actions)
        {
            if (node is ActionSubmenu submenu)
            {
                var grouped = missing.OfType<ActionSubmenu>()
                    .Where(m => m.Name == submenu.Name)
                    .ToList();
                if (grouped.Count == 0)
                {
                    merged.Add(node);
                    continue;
                }
                merged.Add(new ActionSubmenu(
                    submenu.Name,
                    [.. submenu.Actions, .. grouped.SelectMany(g => g.Actions)]));
                missing = missing.Except(grouped).ToList();
            }
            else
            {
                merged.Add(node);
            }
        }
        merged.AddRange(missing);
        return new ActionConfig(merged, new Dictionary<ItemKind, string>(user.Defaults));
    }

    private static IEnumerable<ActionNode> FilterMissing(
        IEnumerable<ActionNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (node is ActionSubmenu submenu)
            {
                var kept = FilterMissing(submenu.Actions, ids).ToList();
                if (kept.Count > 0)
                {
                    yield return submenu with { Actions = kept };
                }
            }
            else if (node is ClipboardAction action)
            {
                if (!ids.Contains(action.Id))
                {
                    yield return node;
                }
            }
        }
    }

    // Badge count for the prefs UI (Copyous `countDifference` parity):
    // how many ids occur in `other` but not in `config`.
    public static int CountDifference(ActionConfig config, ActionConfig other)
    {
        var ids = new HashSet<string>(ActionCatalog.Flatten(config).Select(a => a.Id));
        return ActionCatalog.Flatten(other).Count(a => !ids.Contains(a.Id));
    }
}
