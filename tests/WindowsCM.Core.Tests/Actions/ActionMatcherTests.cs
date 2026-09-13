// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

public sealed class ActionMatcherTests
{
    private static CommandAction Probe(
        string? pattern = null,
        List<ItemKind>? types = null,
        string id = "probe") =>
        new(id, "Probe", "cmd", pattern, types, ActionOutput.Ignore, null);

    [Fact]
    public void Test_EmptyTypes_MatchesAllKinds()
    {
        Assert.True(ActionMatcher.Test(ItemKind.Image, "x", Probe(types: [])));
        Assert.True(ActionMatcher.Test(ItemKind.Color, "red", Probe(types: null)));
    }

    [Fact]
    public void Test_TypeSubset_FiltersFirst()
    {
        var action = Probe(types: [ItemKind.File]);

        Assert.True(ActionMatcher.Test(ItemKind.File, "anything", action));
        Assert.False(ActionMatcher.Test(ItemKind.Files, "anything", action));
    }

    [Fact]
    public void Test_PatternGuards_FollowCopyousDefaults()
    {
        var rgb = BuiltinActions.Default().Actions
            .OfType<ActionSubmenu>().Single(s => s.Name == "Converter")
            .Actions.OfType<ColorAction>().Single(a => a.Id == "rgb");

        Assert.False(ActionMatcher.Test(ItemKind.Color, "rgb(1 2 3)", rgb));
        Assert.True(ActionMatcher.Test(ItemKind.Color, "red", rgb));
    }

    [Fact]
    public void Test_BadPattern_IsNoMatchNeverThrow()
    {
        Assert.False(ActionMatcher.Test(ItemKind.Text, "hello", Probe(pattern: "([a-")));
    }

    [Fact]
    public void Test_CatastrophicPattern_TimesOutToNoMatch()
    {
        var evil = Probe(pattern: "^(a+)+$");

        Assert.False(ActionMatcher.Test(
            ItemKind.Text, new string('a', 30) + "!", evil));
    }

    [Fact]
    public void Match_ReturnsFullMatchPlusGroups()
    {
        var match = ActionMatcher.Match(
            ItemKind.Text, "hello world", Probe(pattern: @"^(\w+) (\w+)$"));

        Assert.NotNull(match);
        Assert.Equal(["hello world", "hello", "world"], match);
    }

    [Fact]
    public void Match_NoPattern_ReturnsContent()
    {
        Assert.Equal(
            ["hello"],
            ActionMatcher.Match(ItemKind.Text, "hello", Probe()));
    }

    [Fact]
    public void Match_TypeMismatch_ReturnsNull()
    {
        Assert.Null(ActionMatcher.Match(
            ItemKind.Text, "x", Probe(types: [ItemKind.File])));
    }

    [Fact]
    public void Match_NoHit_ReturnsNull()
    {
        Assert.Null(ActionMatcher.Match(
            ItemKind.Text, "abc", Probe(pattern: "^z")));
    }

    [Fact]
    public void Match_BadPattern_ReturnsNull()
    {
        Assert.Null(ActionMatcher.Match(
            ItemKind.Text, "abc", Probe(pattern: "([a-")));
    }

    [Fact]
    public void Match_DocumentsUnicodeSemanticsDifference()
    {
        // .NET matches canonically (Unicode), unlike JS ASCII \w: "é"
        // matches here where the GNOME build would not. Ports relying on
        // \w/\b/\d match a superset — by design, not by accident.
        Assert.True(ActionMatcher.Test(ItemKind.Text, "café", Probe(pattern: @"^\w+$")));
    }
}
