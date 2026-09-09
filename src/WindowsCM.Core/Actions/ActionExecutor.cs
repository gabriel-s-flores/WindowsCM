// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

public enum ActionStatus
{
    // Ran with output Ignore (or empty stdout): nothing further to do.
    Done,
    // The result belongs on the clipboard (caller copies).
    Copy,
    // The result belongs on the clipboard and into the target (caller pastes).
    Paste,
    // The UI dialog should show a QR payload (Output carries it).
    ShowQr,
    // The action does not apply to this item (type/regex/QR-kind guard).
    NotApplicable,
    // No default is mapped for this item kind (popup Ctrl+Enter stays silent).
    NoDefault,
    // Ran and failed; Diagnostics explains (shown, never silent).
    Failed,
}

public sealed record ActionResult(ActionStatus Status, string? Output, string? Diagnostics);

// Runs actions against items (Copyous `actionMenu` run parity): the popup
// chord resolves the per-type default (submenus included), commands run
// with content on stdin, colors convert through the ported parser, QR
// hands its payload to the dialog. Copy/Paste results are returned, not
// applied — the caller (popup, issue 15) owns clipboard + injection, like
// Copyous emitting copy/paste signals.
public sealed class ActionExecutor
{
    // Copyous 30s command timeout, preserved as a constant.
    public const int CommandTimeoutMs = 30_000;

    private readonly IProcessRunner _runner;
    private readonly IShellLauncher _shells;

    public ActionExecutor(IProcessRunner runner, IShellLauncher shells)
    {
        _runner = runner;
        _shells = shells;
    }

    // Copyous testAction over the whole config, submenu members inline in
    // depth-first order (drives the popup action menu). Unknown kinds are
    // runnable by nothing, so the menu hides them instead of offering a
    // row that can only fail.
    public IReadOnlyList<ClipboardAction> Applicable(ActionConfig config, ClipboardItem item) =>
        ActionCatalog.Flatten(config)
            .Where(a => a is not UnknownAction)
            .Where(a => ActionMatcher.Test(item.Kind, item.Content, a))
            .ToList();

    public async Task<ActionResult> ExecuteDefaultAsync(
        ActionConfig config, ClipboardItem item, CancellationToken ct = default)
    {
        var action = ActionCatalog.FindDefaultAction(config, item.Kind);
        if (action is null)
        {
            return new ActionResult(ActionStatus.NoDefault, null, null);
        }
        return await ExecuteAsync(action, item, ct).ConfigureAwait(false);
    }

    public Task<ActionResult> ExecuteAsync(
        ClipboardAction action, ClipboardItem item, CancellationToken ct = default) =>
        action switch
        {
            CommandAction command => ExecuteCommandAsync(command, item, ct),
            ColorAction color => Task.FromResult(ExecuteColor(color, item)),
            QrCodeAction qr => Task.FromResult(ExecuteQr(qr, item)),
            _ => Task.FromResult(new ActionResult(
                ActionStatus.Failed, null,
                $"Unsupported action kind '{action.Kind}'; only command, color and qrcode run.")),
        };

    private async Task<ActionResult> ExecuteCommandAsync(
        CommandAction action, ClipboardItem item, CancellationToken ct)
    {
        // Applicability first (Copyous activateDefaultAction parity): the
        // popup chord resolves an action by id, but it only runs when the
        // type subset and pattern still accept the item.
        if (!ActionMatcher.Test(item.Kind, item.Content, action))
        {
            return new ActionResult(ActionStatus.NotApplicable, null, null);
        }
        // The four ported ids run natively (BuiltinActions): shell-open,
        // Explorer reveal, browser open, and the file:// path rewrite.
        // Linux-authored configs (xdg-open, nautilus, cut) migrate free.
        if (action.Id == BuiltinActions.OpenWithDefault)
        {
            return OpenEachLine(item);
        }
        if (action.Id == BuiltinActions.OpenWithFiles)
        {
            return RevealFirstLine(item);
        }
        if (action.Id == BuiltinActions.OpenWithBrowser)
        {
            return OpenUrl(item);
        }
        if (action.Id == BuiltinActions.PasteAsPath)
        {
            return PastePaths(item, action);
        }

        var match = ActionMatcher.Match(item.Kind, item.Content, action);
        if (match is null)
        {
            return new ActionResult(ActionStatus.NotApplicable, null, null);
        }
        // Groups[0] is the full match (Copyous matchAction parity): argv
        // carries groups 1..N with nulls as empty, like `_` + slice(1).
        var groups = match.Skip(1).Select(g => g ?? string.Empty).ToList();
        var request = new ProcessRequest(
            "cmd.exe",
            CommandLine.BuildArguments(action.Command, groups),
            item.Content,
            CommandTimeoutMs);
        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ActionResult(
                ActionStatus.Failed, null, $"Command '{action.Name}' failed to start: {ex.Message}");
        }
        if (result.TimedOut)
        {
            return new ActionResult(
                ActionStatus.Failed, null,
                $"Command '{action.Name}' timed out after 30s and was killed.");
        }
        if (result.ExitCode != 0)
        {
            var detail = result.Stderr.Trim();
            return new ActionResult(
                ActionStatus.Failed, null,
                string.IsNullOrEmpty(detail)
                    ? $"Command '{action.Name}' exited with code {result.ExitCode}."
                    : $"Command '{action.Name}' exited with code {result.ExitCode}: {detail}");
        }
        var output = result.Stdout.Trim();
        if (output.Length == 0)
        {
            return new ActionResult(ActionStatus.Done, null, null);
        }
        return action.Output switch
        {
            ActionOutput.Copy => new ActionResult(ActionStatus.Copy, output, null),
            ActionOutput.Paste => new ActionResult(ActionStatus.Paste, output, null),
            _ => new ActionResult(ActionStatus.Done, null, null),
        };
    }

    private ActionResult OpenEachLine(ClipboardItem item)
    {
        var lines = item.Content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        if (lines.Count == 0)
        {
            return new ActionResult(ActionStatus.Failed, null, "Nothing to open: the item is empty.");
        }
        try
        {
            foreach (var line in lines)
            {
                _shells.Open(PathRewriter.ToLocalPath(line));
            }
            return new ActionResult(ActionStatus.Done, null, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ActionResult(ActionStatus.Failed, null, $"Open failed: {ex.Message}");
        }
    }

    private ActionResult RevealFirstLine(ClipboardItem item)
    {
        // ^(.*) parity: the first capture is the first line; multi-file
        // reveals select it, exactly like `nautilus -s $1` did.
        var first = item.Content.Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
        if (first is null)
        {
            return new ActionResult(ActionStatus.Failed, null, "Nothing to reveal: the item is empty.");
        }
        try
        {
            _shells.Reveal(PathRewriter.ToLocalPath(first));
            return new ActionResult(ActionStatus.Done, null, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ActionResult(ActionStatus.Failed, null, $"Reveal failed: {ex.Message}");
        }
    }

    private ActionResult OpenUrl(ClipboardItem item)
    {
        var url = item.Content.Trim();
        if (url.Length == 0)
        {
            return new ActionResult(ActionStatus.Failed, null, "Nothing to open: the item is empty.");
        }
        try
        {
            _shells.Open(url);
            return new ActionResult(ActionStatus.Done, null, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ActionResult(ActionStatus.Failed, null, $"Open failed: {ex.Message}");
        }
    }

    private static ActionResult PastePaths(ClipboardItem item, CommandAction action)
    {
        var paths = PathRewriter.ToLocalPaths(item.Content);
        if (paths.Length == 0)
        {
            return new ActionResult(ActionStatus.Done, null, null);
        }
        return action.Output switch
        {
            ActionOutput.Copy => new ActionResult(ActionStatus.Copy, paths, null),
            ActionOutput.Paste => new ActionResult(ActionStatus.Paste, paths, null),
            _ => new ActionResult(ActionStatus.Done, null, null),
        };
    }

    private static ActionResult ExecuteColor(ColorAction action, ClipboardItem item)
    {
        if (!ActionMatcher.Test(item.Kind, item.Content, action))
        {
            return new ActionResult(ActionStatus.NotApplicable, null, null);
        }
        var parsed = ColorParser.TryParse(item.Content.Trim());
        if (parsed is null)
        {
            return new ActionResult(
                ActionStatus.Failed, null,
                $"Color conversion '{action.Name}' cannot parse the item content.");
        }
        var converted = ColorConverter.Format(ColorConverter.Convert(parsed, action.Space));
        return action.Output switch
        {
            ActionOutput.Copy => new ActionResult(ActionStatus.Copy, converted, null),
            ActionOutput.Paste => new ActionResult(ActionStatus.Paste, converted, null),
            _ => new ActionResult(ActionStatus.Done, null, null),
        };
    }

    private static ActionResult ExecuteQr(QrCodeAction action, ClipboardItem item)
    {
        if (!ActionMatcher.Test(item.Kind, item.Content, action))
        {
            return new ActionResult(ActionStatus.NotApplicable, null, null);
        }
        var payload = QrActions.Payload(item.Kind, item.Content);
        if (payload is null)
        {
            return new ActionResult(
                ActionStatus.NotApplicable, null, null);
        }
        return new ActionResult(ActionStatus.ShowQr, payload, null);
    }
}
