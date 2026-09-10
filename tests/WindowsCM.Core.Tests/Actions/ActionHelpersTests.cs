// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

public sealed class CommandLineTests
{
    [Fact]
    public void Substitute_PercentPlaceholders_InOrder()
    {
        Assert.Equal(
            @"explorer.exe /select,""C:\a.txt""",
            CommandLine.Substitute(@"explorer.exe /select,""%1""", [@"C:\a.txt"]));
    }

    [Fact]
    public void Substitute_DollarAlias_KeepsLinuxPortsWorking()
    {
        Assert.Equal(
            "open C:/a.txt and C:/b.txt",
            CommandLine.Substitute("open $1 and $2", ["C:/a.txt", "C:/b.txt"]));
    }

    [Fact]
    public void Substitute_MissingGroup_ExpandsToEmpty()
    {
        Assert.Equal("run only !", CommandLine.Substitute("run %1 %2!", ["only"]));
    }

    [Fact]
    public void Substitute_NoGroups_ReturnsCommandVerbatim()
    {
        Assert.Equal("100% sure $1", CommandLine.Substitute("100% sure $1", []));
    }

    [Fact]
    public void Substitute_LoneMarkers_LeftAlone()
    {
        Assert.Equal("100% and $", CommandLine.Substitute("100% and $", ["x"]));
    }

    [Fact]
    public void BuildArguments_AppendsGroupsAsTrailingArgv()
    {
        Assert.Equal(
            "/c mytool a \"b c\"",
            CommandLine.BuildArguments("mytool", ["a", "b c"]));
    }

    [Fact]
    public void BuildArguments_WithoutGroups_JustPrefixes()
    {
        Assert.Equal("/c echo hi", CommandLine.BuildArguments("echo hi", []));
    }
}

public sealed class PathRewriterTests
{
    [Fact]
    public void ToLocalPath_StripsSchemeAndUnescapes()
    {
        Assert.Equal(
            "C:/My Docs/a.txt",
            PathRewriter.ToLocalPath("file:///C:/My%20Docs/a.txt"));
    }

    [Fact]
    public void ToLocalPath_PlainPath_PassesThrough()
    {
        Assert.Equal(@"C:\a.txt", PathRewriter.ToLocalPath(@"C:\a.txt"));
    }

    [Fact]
    public void ToLocalPath_TrimsWhitespace_PreservesTrailingSlash()
    {
        Assert.Equal("C:/a.txt/", PathRewriter.ToLocalPath("  file:///C:/a.txt/  "));
    }

    [Fact]
    public void ToLocalPath_EmbeddedScheme_Survives()
    {
        Assert.Equal(
            "C:/dir/file://weird.txt",
            PathRewriter.ToLocalPath("file:///C:/dir/file://weird.txt"));
    }

    [Fact]
    public void ToLocalPath_MalformedEscape_FallsBackWithoutThrowing()
    {
        Assert.Equal("C:/a%.txt", PathRewriter.ToLocalPath("file:///C:/a%.txt"));
    }

    [Fact]
    public void ToLocalPaths_MultilineMapsEachLine_DroppingBlanks()
    {
        Assert.Equal(
            "C:/a.txt\nC:/b.txt",
            PathRewriter.ToLocalPaths("file:///C:/a.txt\n\nfile:///C:/b.txt\n"));
    }

    [Fact]
    public void ToLocalPaths_EmptyContent_StaysEmpty()
    {
        Assert.Equal(string.Empty, PathRewriter.ToLocalPaths("  \n "));
    }
}

public sealed class QrActionsTests
{
    [Theory]
    [InlineData(ItemKind.Text)]
    [InlineData(ItemKind.Code)]
    [InlineData(ItemKind.Link)]
    [InlineData(ItemKind.Character)]
    [InlineData(ItemKind.Color)]
    public void SupportedKinds_AreTheFiveTextLike(ItemKind kind)
    {
        Assert.True(QrActions.IsSupported(kind));
        Assert.Equal("payload", QrActions.Payload(kind, "payload"));
    }

    [Theory]
    [InlineData(ItemKind.Image)]
    [InlineData(ItemKind.File)]
    [InlineData(ItemKind.Files)]
    public void UnsupportedKinds_HaveNoPayload(ItemKind kind)
    {
        Assert.False(QrActions.IsSupported(kind));
        Assert.Null(QrActions.Payload(kind, "payload"));
    }

    [Fact]
    public void EmptyContent_HasNoPayload()
    {
        Assert.Null(QrActions.Payload(ItemKind.Text, string.Empty));
    }
}

public sealed class ActionValidationTests
{
    [Fact]
    public void Command_RequiresNameAndCommand()
    {
        Assert.NotNull(ActionValidation.Validate(
            new CommandAction("a", "", "x", null, null, ActionOutput.Copy, null)));
        Assert.NotNull(ActionValidation.Validate(
            new CommandAction("a", "N", "  ", null, null, ActionOutput.Copy, null)));
        Assert.Null(ActionValidation.Validate(
            new CommandAction("a", "N", "x", null, null, ActionOutput.Copy, null)));
    }

    [Fact]
    public void ColorAndQr_RequireName()
    {
        Assert.NotNull(ActionValidation.Validate(
            new ColorAction("a", "", WindowsCM.Core.Classification.ColorSpace.Hex, null, null, ActionOutput.Paste, null)));
        Assert.NotNull(ActionValidation.Validate(
            new QrCodeAction("a", "", null, null, null)));
        Assert.Null(ActionValidation.Validate(
            new QrCodeAction("a", "QR", null, null, null)));
    }

    [Fact]
    public void Submenu_RequiresName()
    {
        Assert.NotNull(ActionValidation.Validate(new ActionSubmenu("", [])));
        Assert.Null(ActionValidation.Validate(new ActionSubmenu("Open", [])));
    }

    [Fact]
    public void NewId_ReturnsUniqueValues()
    {
        Assert.NotEqual(ActionValidation.NewId(), ActionValidation.NewId());
        Assert.True(Guid.TryParse(ActionValidation.NewId(), out _));
    }
}
