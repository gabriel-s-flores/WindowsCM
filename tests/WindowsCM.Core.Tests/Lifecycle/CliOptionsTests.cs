// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class CliOptionsTests
{
    [Fact]
    public void Parse_SkipsExePath()
    {
        var options = CliOptions.Parse(["C:\\apps\\WindowsCM.exe", "--toggle"]);

        Assert.Equal(IpcCommand.Toggle, options.Command);
        Assert.False(options.StartHidden);
    }

    [Theory]
    [InlineData("--show", IpcCommand.Show)]
    [InlineData("--hide", IpcCommand.Hide)]
    [InlineData("--toggle", IpcCommand.Toggle)]
    [InlineData("--clear", IpcCommand.Clear)]
    [InlineData("--clear-all", IpcCommand.ClearAll)]
    [InlineData("--ping", IpcCommand.Ping)]
    public void Parse_MapsCommands(string flag, IpcCommand expected)
    {
        Assert.Equal(expected, CliOptions.Parse([flag]).Command);
    }

    [Theory]
    [InlineData("-show")]
    [InlineData("/show")]
    [InlineData("--SHOW")]
    public void Parse_AcceptsPrefixAndCaseVariants(string flag)
    {
        Assert.Equal(IpcCommand.Show, CliOptions.Parse([flag]).Command);
    }

    [Fact]
    public void Parse_AcceptsUnderscoreAlias()
    {
        Assert.Equal(IpcCommand.ClearAll, CliOptions.Parse(["--clear_all"]).Command);
    }

    [Fact]
    public void Parse_HiddenComposesWithCommand()
    {
        var options = CliOptions.Parse(["--hidden", "--toggle"]);

        Assert.Equal(IpcCommand.Toggle, options.Command);
        Assert.True(options.StartHidden);
    }

    [Fact]
    public void Parse_HiddenAlone_HasNoCommand()
    {
        var options = CliOptions.Parse(["app.exe", "--hidden"]);

        Assert.Null(options.Command);
        Assert.True(options.StartHidden);
    }

    [Fact]
    public void Parse_FirstCommandWins()
    {
        var options = CliOptions.Parse(["--show", "--hide"]);

        Assert.Equal(IpcCommand.Show, options.Command);
    }

    [Fact]
    public void Parse_UnknownTokensIgnored()
    {
        var options = CliOptions.Parse(["app.exe", "--frobnicate", "positional"]);

        Assert.Null(options.Command);
        Assert.False(options.StartHidden);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_HelpFlags()
    {
        Assert.True(CliOptions.Parse(["--help"]).ShowHelp);
        Assert.True(CliOptions.Parse(["-h"]).ShowHelp);
        Assert.True(CliOptions.Parse(["/?"]).ShowHelp);
    }

    [Fact]
    public void FormatForForward_NoCommandToggles()
    {
        Assert.Equal("toggle", CliOptions.Parse(["app.exe"]).FormatForForward());
        Assert.Equal("show", CliOptions.Parse(["--show"]).FormatForForward());
    }

    [Fact]
    public void FormatCommand_NoCommandIsNull()
    {
        Assert.Null(CliOptions.Parse(["app.exe"]).FormatCommand());
    }
}
