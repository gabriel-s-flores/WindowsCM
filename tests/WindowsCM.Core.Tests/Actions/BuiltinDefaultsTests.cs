// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

public sealed class BuiltinDefaultsTests
{
    [Fact]
    public void Default_OpenSubmenu_HoldsThreePortedCommands()
    {
        var config = BuiltinActions.Default();
        var open = config.Actions.OfType<ActionSubmenu>()
            .Single(s => s.Name == "Abrir");

        var ids = open.Actions.OfType<CommandAction>().Select(a => a.Id).ToList();
        Assert.Equal(
            [BuiltinActions.OpenWithDefault, BuiltinActions.OpenWithFiles, BuiltinActions.OpenWithBrowser],
            ids);
    }

    [Fact]
    public void Default_Commands_AreWindowsNotGnome()
    {
        var commands = ActionCatalog.Flatten(BuiltinActions.Default())
            .OfType<CommandAction>()
            .Select(a => a.Command)
            .ToList();

        Assert.DoesNotContain(commands, c => c.Contains("xdg-open"));
        Assert.DoesNotContain(commands, c => c.Contains("nautilus"));
        Assert.DoesNotContain(commands, c => c.Contains("cut -c8-"));
        // Explorer reveal keeps the cmd-style placeholder (not $1).
        var reveal = ActionCatalog.Flatten(BuiltinActions.Default())
            .OfType<CommandAction>()
            .Single(a => a.Id == BuiltinActions.OpenWithFiles);
        Assert.Contains("%1", reveal.Command);
        Assert.DoesNotContain("$1", reveal.Command);
        Assert.Contains("explorer.exe", reveal.Command);
        Assert.Contains("Start-Process",
            string.Join("\n", commands.Where(c => c.Contains("powershell"))));
    }

    [Fact]
    public void Default_ConvertSubmenu_HasFourActiveGuards()
    {
        var config = BuiltinActions.Default();
        var convert = config.Actions.OfType<ActionSubmenu>()
            .Single(s => s.Name == "Converter");

        var colors = convert.Actions.OfType<ColorAction>().ToList();
        Assert.Equal(["rgb", "hex", "hsl", "oklch"],
            colors.Select(a => a.Id).ToList());
        Assert.Equal(["^(?!rgb)", "^(?!#)", "^(?!hsl)", "^(?!oklch)"],
            colors.Select(a => a.Pattern).ToList());
        Assert.All(colors, c =>
        {
            Assert.Equal([ItemKind.Color], c.Types);
            Assert.Equal(ActionOutput.Paste, c.Output);
        });
    }

    [Fact]
    public void Default_QrCode_TextLikeKindsOnCtrlQ()
    {
        var qr = ActionCatalog.FindById(BuiltinActions.Default(), BuiltinActions.QrCode);

        var action = Assert.IsType<QrCodeAction>(qr);
        Assert.Equal(["Ctrl+Q"], action.Shortcut);
        Assert.Equal(
            [ItemKind.Text, ItemKind.Code, ItemKind.Link, ItemKind.Character, ItemKind.Color],
            action.Types);
        Assert.Equal(ActionOutput.Ignore, action.Output);
    }

    [Fact]
    public void Default_PerTypeDefaults_FileFilesLink()
    {
        var config = BuiltinActions.Default();

        Assert.Equal(BuiltinActions.PasteAsPath, config.Defaults[ItemKind.File]);
        Assert.Equal(BuiltinActions.PasteAsPath, config.Defaults[ItemKind.Files]);
        Assert.Equal(BuiltinActions.OpenWithBrowser, config.Defaults[ItemKind.Link]);
        Assert.False(config.Defaults.ContainsKey(ItemKind.Text));
    }

    [Fact]
    public void FindById_ResolvesInsideSubmenus()
    {
        // open-with-browser lives inside Open, not at top level.
        var found = ActionCatalog.FindById(
            BuiltinActions.Default(), BuiltinActions.OpenWithBrowser);

        Assert.NotNull(found);
        Assert.Equal(BuiltinActions.OpenWithBrowser, found.Id);
    }

    [Fact]
    public void IsDefaultAction_MatchesConfiguredMapping()
    {
        var config = BuiltinActions.Default();
        var browser = ActionCatalog.FindById(config, BuiltinActions.OpenWithBrowser)!;
        var paste = ActionCatalog.FindById(config, BuiltinActions.PasteAsPath)!;

        Assert.True(ActionCatalog.IsDefaultAction(config, ItemKind.Link, browser));
        Assert.False(ActionCatalog.IsDefaultAction(config, ItemKind.File, browser));
        Assert.True(ActionCatalog.IsDefaultAction(config, ItemKind.Files, paste));
    }

    [Fact]
    public void FindDefaultAction_UnknownId_ReturnsNull()
    {
        var config = BuiltinActions.Default() with
        {
            Defaults = new Dictionary<ItemKind, string> { [ItemKind.Text] = "no-such-action" },
        };

        Assert.Null(ActionCatalog.FindDefaultAction(config, ItemKind.Text));
        Assert.Null(ActionCatalog.FindDefaultAction(config, ItemKind.Code));
    }
}
