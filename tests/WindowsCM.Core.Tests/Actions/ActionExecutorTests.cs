// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

// OS edges faked (spec Testing Decisions): the process runner and the
// shell launcher are fakes asserting argv/stdin/timeout and paths; color,
// QR and matching stay pure.
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessRequest> Requests = [];
    public ProcessResult Next = new(0, "out", string.Empty, false);
    public Exception? Throw;

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        if (Throw is not null)
        {
            throw Throw;
        }
        return Task.FromResult(Next);
    }
}

internal sealed class FakeShellLauncher : IShellLauncher
{
    public List<string> Opened = [];
    public List<string> Revealed = [];

    public void Open(string pathOrUrl) => Opened.Add(pathOrUrl);
    public void Reveal(string localPath) => Revealed.Add(localPath);
}

public sealed class ActionExecutorTests
{
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeShellLauncher _shells = new();
    private ActionExecutor Subject() => new(_runner, _shells);

    private static readonly DateTime T0 =
        new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    private static ClipboardItem Item(ItemKind kind, string content) =>
        new(kind, content, false, null, T0, null, null);

    private static CommandAction CustomCommand(
        string command = "mytool %1",
        string? pattern = "^(.*)",
        ActionOutput output = ActionOutput.Copy) =>
        new("custom-1", "Custom", command, pattern, [ItemKind.Text], output, null);

    [Fact]
    public async Task CustomCommand_RunsWithContentOnStdin_CapturesTrimmedStdout()
    {
        _runner.Next = new ProcessResult(0, "  out  \n", string.Empty, false);

        var result = await Subject().ExecuteAsync(
            CustomCommand(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Copy, result.Status);
        Assert.Equal("out", result.Output);
        var request = Assert.Single(_runner.Requests);
        Assert.Equal("cmd.exe", request.FileName);
        Assert.StartsWith("/c ", request.Arguments);
        Assert.Contains("mytool hello", request.Arguments);
        Assert.Equal("hello", request.StandardInput);
        Assert.Equal(ActionExecutor.CommandTimeoutMs, request.TimeoutMs);
    }

    [Fact]
    public async Task CustomCommand_PlaceholdersSubstituteGroups()
    {
        var action = new CommandAction(
            "custom-1", "Custom", "diff %1 %2", @"^(\w+)@(\w+)$",
            [ItemKind.Text], ActionOutput.Ignore, null);

        var result = await Subject().ExecuteAsync(action, Item(ItemKind.Text, "a@b"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Contains("diff a b", _runner.Requests.Single().Arguments);
    }

    [Fact]
    public async Task CustomCommand_DollarAlias_WorksForLinuxPorts()
    {
        var action = CustomCommand("open $1", "^(.*)");

        await Subject().ExecuteAsync(action, Item(ItemKind.Text, "hello"));

        Assert.Contains("open hello", _runner.Requests.Single().Arguments);
    }

    [Fact]
    public async Task CustomCommand_PasteOutput_ReturnsPaste()
    {
        _runner.Next = new ProcessResult(0, "pasted", string.Empty, false);

        var result = await Subject().ExecuteAsync(
            CustomCommand(output: ActionOutput.Paste), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Paste, result.Status);
        Assert.Equal("pasted", result.Output);
    }

    [Fact]
    public async Task CustomCommand_EmptyStdout_IsDone()
    {
        _runner.Next = new ProcessResult(0, "   \n", string.Empty, false);

        var result = await Subject().ExecuteAsync(
            CustomCommand(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task CustomCommand_NonZeroExit_FailsWithStderr()
    {
        _runner.Next = new ProcessResult(1, "out", "boom", false);

        var result = await Subject().ExecuteAsync(
            CustomCommand(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Contains("boom", result.Diagnostics);
    }

    [Fact]
    public async Task CustomCommand_Timeout_FailsAfterKill()
    {
        _runner.Next = new ProcessResult(0, string.Empty, string.Empty, TimedOut: true);

        var result = await Subject().ExecuteAsync(
            CustomCommand(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Contains("30s", result.Diagnostics);
    }

    [Fact]
    public async Task CustomCommand_PatternMismatch_NeverSpawns()
    {
        var result = await Subject().ExecuteAsync(
            CustomCommand(pattern: "^z"), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.NotApplicable, result.Status);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task CustomCommand_StartFailure_FailsWithDiagnostics()
    {
        _runner.Throw = new InvalidOperationException("pipe burst");

        var result = await Subject().ExecuteAsync(
            CustomCommand(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Contains("pipe burst", result.Diagnostics);
    }

    [Fact]
    public async Task PasteAsPath_RewritesWithoutSpawning()
    {
        var action = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.PasteAsPath)!;
        var item = Item(ItemKind.Files, "file:///C:/a.txt\nfile:///C:/b.txt");

        var result = await Subject().ExecuteAsync(action, item);

        Assert.Equal(ActionStatus.Paste, result.Status);
        Assert.Equal("C:/a.txt\nC:/b.txt", result.Output);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task OpenWithDefault_OpensRewrittenLocalPath()
    {
        var action = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.OpenWithDefault)!;

        var result = await Subject().ExecuteAsync(
            action, Item(ItemKind.File, "file:///C:/My%20Docs/a.txt"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Equal(["C:/My Docs/a.txt"], _shells.Opened);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task OpenWithFiles_RevealsFirstPathOnly()
    {
        var action = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.OpenWithFiles)!;

        var result = await Subject().ExecuteAsync(
            action, Item(ItemKind.Files, "file:///C:/a.txt\nfile:///C:/b.txt"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Equal(["C:/a.txt"], _shells.Revealed);
        Assert.Empty(_shells.Opened);
    }

    [Fact]
    public async Task OpenWithBrowser_OpensUrlVerbatim()
    {
        var action = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.OpenWithBrowser)!;

        var result = await Subject().ExecuteAsync(
            action, Item(ItemKind.Link, "https://example.com/x?y=1"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Equal(["https://example.com/x?y=1"], _shells.Opened);
    }

    [Fact]
    public async Task ExecuteDefault_FileItem_PastesPaths()
    {
        var result = await Subject().ExecuteDefaultAsync(
            BuiltinActions.Default(), Item(ItemKind.File, "file:///C:/a.txt"));

        Assert.Equal(ActionStatus.Paste, result.Status);
        Assert.Equal("C:/a.txt", result.Output);
    }

    [Fact]
    public async Task ExecuteDefault_LinkItem_ResolvesInsideOpenSubmenu()
    {
        var result = await Subject().ExecuteDefaultAsync(
            BuiltinActions.Default(), Item(ItemKind.Link, "https://example.com"));

        Assert.Equal(ActionStatus.Done, result.Status);
        Assert.Equal(["https://example.com"], _shells.Opened);
    }

    [Fact]
    public async Task ExecuteDefault_TextItem_HasNoDefault()
    {
        var result = await Subject().ExecuteDefaultAsync(
            BuiltinActions.Default(), Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.NoDefault, result.Status);
        Assert.Empty(_runner.Requests);
        Assert.Empty(_shells.Opened);
    }

    [Fact]
    public async Task ExecuteDefault_MappedButInapplicable_StaysSilent()
    {
        var config = BuiltinActions.Default() with
        {
            Defaults = new Dictionary<ItemKind, string>
            {
                [ItemKind.Text] = BuiltinActions.OpenWithBrowser, // Link-only action
            },
        };

        var result = await Subject().ExecuteDefaultAsync(
            config, Item(ItemKind.Text, "hello"));

        Assert.Equal(ActionStatus.NotApplicable, result.Status);
        Assert.Empty(_shells.Opened);
    }

    [Fact]
    public async Task ColorAction_ConvertsThroughParser()
    {
        var hex = BuiltinActions.Default().Actions
            .OfType<ActionSubmenu>().Single(s => s.Name == "Converter")
            .Actions.OfType<ColorAction>().Single(a => a.Id == "hex");

        var result = await Subject().ExecuteAsync(hex, Item(ItemKind.Color, "red"));

        Assert.Equal(ActionStatus.Paste, result.Status);
        Assert.Equal("#ff0000", result.Output);
    }

    [Fact]
    public void Applicable_ListsMatchingInline_ExcludingOthers()
    {
        var applicable = Subject().Applicable(
            BuiltinActions.Default(), Item(ItemKind.Color, "red"));

        Assert.Contains(applicable, a => a.Id == "hex");
        Assert.Contains(applicable, a => a.Id == BuiltinActions.QrCode);
        Assert.DoesNotContain(applicable, a => a.Id == BuiltinActions.OpenWithBrowser);
    }

    [Fact]
    public async Task QrAction_TextItem_HandsPayloadToDialog()
    {
        var qr = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.QrCode)!;

        var result = await Subject().ExecuteAsync(qr, Item(ItemKind.Text, "hello phone"));

        Assert.Equal(ActionStatus.ShowQr, result.Status);
        Assert.Equal("hello phone", result.Output);
        Assert.Equal(ActionOutput.Ignore,
            ((QrCodeAction)qr).Output);
    }

    [Fact]
    public async Task QrAction_ImageItem_IsNotApplicable()
    {
        var qr = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.QrCode)!;

        var result = await Subject().ExecuteAsync(
            qr, Item(ItemKind.Image, "file:///img.png"));

        Assert.Equal(ActionStatus.NotApplicable, result.Status);
    }

    [Fact]
    public void Applicable_HidesUnknownKinds()
    {
        var config = new ActionConfig(
            [
                new UnknownAction("teleport", "t", "Beam", null, null, ActionOutput.Ignore, null),
                .. BuiltinActions.Default().Actions,
            ],
            new Dictionary<ItemKind, string>());

        var applicable = Subject().Applicable(config, Item(ItemKind.Text, "hello"));

        Assert.DoesNotContain(applicable, a => a.Id == "t");
    }

    [Fact]
    public async Task UnknownKind_FailsWithDiagnostics()
    {
        var unknown = new UnknownAction(
            "teleport", "t", "Beam", null, null, ActionOutput.Ignore, null);

        var result = await Subject().ExecuteAsync(unknown, Item(ItemKind.Text, "x"));

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Contains("teleport", result.Diagnostics);
    }
}
