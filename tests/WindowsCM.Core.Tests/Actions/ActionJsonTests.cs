// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

public sealed class ActionJsonTests
{
    // Record lists break value equality, so round-trips compare through
    // the canonical serialization instead.
    private static void AssertRoundTrips(ActionConfig config) =>
        Assert.Equal(
            ActionsJson.Serialize(config),
            ActionsJson.Serialize(ActionsJson.Deserialize(ActionsJson.Serialize(config))));

    [Fact]
    public void BuiltinConfig_RoundTrips()
    {
        AssertRoundTrips(BuiltinActions.Default());
    }

    [Fact]
    public void Deserialize_AllKinds_SubmenusAndDefaults()
    {
        const string json = """
            {
              "actions": [
                { "name": "Open", "actions": [
                  { "kind": "command", "id": "a", "name": "A",
                    "command": "cat %1", "pattern": "^(.*)",
                    "types": ["File"], "output": "copy", "shortcut": [] }
                ] },
                { "kind": "color", "id": "c", "name": "Hex",
                  "space": "hex", "pattern": "^(?!#)",
                  "types": ["Color"], "output": "paste", "shortcut": [] },
                { "kind": "qrcode", "id": "q", "name": "QR",
                  "pattern": null, "types": ["Text"], "output": "ignore",
                  "shortcut": ["Ctrl+Q"] }
              ],
              "defaults": { "File": "a" }
            }
            """;
        var config = ActionsJson.Deserialize(json);

        var submenu = Assert.IsType<ActionSubmenu>(config.Actions[0]);
        var command = Assert.IsType<CommandAction>(submenu.Actions[0]);
        Assert.Equal("cat %1", command.Command);
        Assert.Equal([ItemKind.File], command.Types);
        Assert.Equal(ActionOutput.Copy, command.Output);
        var color = Assert.IsType<ColorAction>(config.Actions[1]);
        Assert.Equal(ColorSpace.Hex, color.Space);
        var qr = Assert.IsType<QrCodeAction>(config.Actions[2]);
        Assert.Equal(ActionOutput.Ignore, qr.Output);
        Assert.Equal("a", config.Defaults[ItemKind.File]);
        AssertRoundTrips(config);
    }

    [Fact]
    public void Deserialize_ToleratesCommentsTrailingCommasAndCase()
    {
        const string json = """
            {
              // a comment the strict parser would reject
              "actions": [
                { "KIND": "command", "ID": "a", "NAME": "A",
                  "COMMAND": "cat", "OUTPUT": "Copy", },
              ],
              "defaults": { "file": "a", },
            }
            """;
        var config = ActionsJson.Deserialize(json);

        var command = Assert.IsType<CommandAction>(config.Actions.Single());
        Assert.Equal(ActionOutput.Copy, command.Output);
        Assert.Equal("a", config.Defaults[ItemKind.File]);
    }

    [Fact]
    public void Deserialize_UnknownKind_SurvivesAsUnknownAction()
    {
        const string json = """
            { "actions": [
              { "kind": "teleport", "id": "t", "name": "Beam",
                "pattern": null, "types": [], "output": "ignore", "shortcut": [] }
            ] }
            """;
        var config = ActionsJson.Deserialize(json);

        var unknown = Assert.IsType<UnknownAction>(config.Actions.Single());
        Assert.Equal("teleport", unknown.Kind);
        AssertRoundTrips(config);
    }

    [Fact]
    public void Deserialize_UnknownProperties_IgnoredForwardCompat()
    {
        const string json = """
            { "actions": [
              { "kind": "command", "id": "a", "name": "A", "command": "cat",
                "output": "copy", "futureField": { "nested": true } }
            ], "futureTop": 1 }
            """;
        var config = ActionsJson.Deserialize(json);

        Assert.IsType<CommandAction>(config.Actions.Single());
    }

    [Fact]
    public void Deserialize_UnknownTypeName_DroppedOutputFallsBackToIgnore()
    {
        const string json = """
            { "actions": [
              { "kind": "command", "id": "a", "name": "A", "command": "cat",
                "types": ["File", "Telekinesis"], "output": "dematerialize" }
            ] }
            """;
        var config = ActionsJson.Deserialize(json);

        var command = Assert.IsType<CommandAction>(config.Actions.Single());
        Assert.Equal([ItemKind.File], command.Types);
        Assert.Equal(ActionOutput.Ignore, command.Output);
    }

    [Fact]
    public void Deserialize_UnknownColorSpace_BecomesUnknownAction()
    {
        const string json = """
            { "actions": [
              { "kind": "color", "id": "c", "name": "Weird",
                "space": "teleport-green", "types": ["Color"], "output": "paste" }
            ] }
            """;
        var config = ActionsJson.Deserialize(json);

        Assert.IsType<UnknownAction>(config.Actions.Single());
    }

    [Fact]
    public void Deserialize_MissingActions_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => ActionsJson.Deserialize("""{ "defaults": {} }"""));
    }

    [Fact]
    public void Deserialize_MissingRequiredField_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => ActionsJson.Deserialize(
                """{ "actions": [{ "kind": "command", "name": "NoId", "command": "x", "output": "copy" }] }"""));
    }
}
