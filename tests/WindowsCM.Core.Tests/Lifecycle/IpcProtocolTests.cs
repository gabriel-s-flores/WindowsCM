// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class IpcProtocolTests
{
    [Theory]
    [InlineData(IpcCommand.Toggle, "toggle")]
    [InlineData(IpcCommand.Show, "show")]
    [InlineData(IpcCommand.Hide, "hide")]
    [InlineData(IpcCommand.Clear, "clear")]
    [InlineData(IpcCommand.ClearAll, "clear-all")]
    [InlineData(IpcCommand.Ping, "ping")]
    public void Format_UsesLowercaseWireWords(IpcCommand command, string wire)
    {
        Assert.Equal(wire, IpcProtocol.Format(command));
    }

    [Theory]
    [InlineData("toggle", IpcCommand.Toggle)]
    [InlineData("show", IpcCommand.Show)]
    [InlineData("hide", IpcCommand.Hide)]
    [InlineData("clear", IpcCommand.Clear)]
    [InlineData("clear-all", IpcCommand.ClearAll)]
    [InlineData("ping", IpcCommand.Ping)]
    public void TryParse_KnownWords(string line, IpcCommand expected)
    {
        Assert.True(IpcProtocol.TryParse(line, out var command));
        Assert.Equal(expected, command);
    }

    [Theory]
    [InlineData("TOGGLE")]
    [InlineData("  show  ")]
    [InlineData("Clear-All")]
    [InlineData("PING\n")]
    public void TryParse_IsCaseInsensitiveAndTrims(string line)
    {
        Assert.True(IpcProtocol.TryParse(line, out _));
    }

    [Fact]
    public void TryParse_AcceptsUnderscoreAlias()
    {
        Assert.True(IpcProtocol.TryParse("clear_all", out var command));
        Assert.Equal(IpcCommand.ClearAll, command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("copy 12")]
    [InlineData("toggle now")]
    [InlineData("json")]
    public void TryParse_UnknownOrEmpty_ReturnsFalse(string? line)
    {
        Assert.False(IpcProtocol.TryParse(line, out _));
    }

    [Fact]
    public void Responses_AreFixedWords()
    {
        Assert.Equal("ok", IpcProtocol.Ok);
        Assert.Equal("unknown", IpcProtocol.Unknown);
    }
}
