// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.Actions;

public sealed class ActionsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-" + Guid.NewGuid());
    private string PathFor(string name = "actions.json") => Path.Combine(_dir, name);

    public void Dispose()
    {
        foreach (var variable in new[] { ActionsPaths.EnvVariable, ActionsPaths.LegacyEnvVariable })
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsBuiltinsWithoutTouchingDisk()
    {
        var config = ActionsStore.Load(PathFor());

        Assert.Equal(ActionsJson.Serialize(BuiltinActions.Default()), ActionsJson.Serialize(config));
        Assert.False(File.Exists(PathFor()));
    }

    [Fact]
    public void Load_MissingFileWithSaveDefault_WritesTabIndentedFile()
    {
        var config = ActionsStore.Load(PathFor(), saveDefault: true);

        Assert.True(File.Exists(PathFor()));
        var text = File.ReadAllText(PathFor());
        Assert.Contains("\n\t", text);
        Assert.DoesNotContain("\n  ", text);
        Assert.Equal(ActionsJson.Serialize(config), ActionsJson.Serialize(ActionsJson.Deserialize(text)));
    }

    [Fact]
    public void Save_Load_RoundTripsCustomActions()
    {
        var custom = new CommandAction(
            Guid.NewGuid().ToString(), "Mine", "mytool %1", "^(.*)",
            [ItemKind.Text], ActionOutput.Copy, ["Ctrl+M"]);
        var config = BuiltinActions.Default() with
        {
            Actions = [.. BuiltinActions.Default().Actions, custom],
        };

        ActionsStore.Save(PathFor(), config);
        var reloaded = ActionsStore.Load(PathFor());

        Assert.Equal(ActionsJson.Serialize(config), ActionsJson.Serialize(reloaded));
    }

    [Fact]
    public void Load_CorruptFile_DegradesToBuiltins()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathFor(), "{ not json");

        var config = ActionsStore.Load(PathFor());

        Assert.Equal(ActionsJson.Serialize(BuiltinActions.Default()), ActionsJson.Serialize(config));
    }

    [Fact]
    public void Load_SentinelDefault_ReturnsBuiltinsWithoutDisk()
    {
        var config = ActionsStore.Load("default");

        Assert.Equal(ActionsJson.Serialize(BuiltinActions.Default()), ActionsJson.Serialize(config));
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Resolve_EnvironmentBeatsSetting_DatabasePathsParity()
    {
        Environment.SetEnvironmentVariable(ActionsPaths.EnvVariable, @"C:\env\actions.json");

        Assert.Equal(@"C:\env\actions.json", ActionsPaths.Resolve(@"C:\custom\actions.json"));
        Assert.Equal(@"C:\env\actions.json", ActionsPaths.Resolve(null));
    }

    [Fact]
    public void Resolve_LegacyVariable_AsFallback()
    {
        Environment.SetEnvironmentVariable(ActionsPaths.LegacyEnvVariable, @"C:\legacy\actions.json");

        Assert.Equal(@"C:\legacy\actions.json", ActionsPaths.Resolve(null));
    }

    [Fact]
    public void Restore_MergesMissingBuiltins_PreservingCustomsAndDefaults()
    {
        var custom = new CommandAction(
            Guid.NewGuid().ToString(), "Mine", "mytool", null,
            [ItemKind.Text], ActionOutput.Copy, null);
        var user = new ActionConfig(
            [custom],
            new Dictionary<ItemKind, string> { [ItemKind.Text] = custom.Id });

        var restored = ActionsStore.Restore(user);

        // The custom action and the user's default survive…
        Assert.Contains(ActionCatalog.Flatten(restored), a => a.Id == custom.Id);
        Assert.Equal(custom.Id, restored.Defaults[ItemKind.Text]);
        // …while every built-in id is back.
        foreach (var builtin in ActionCatalog.Flatten(BuiltinActions.Default()))
        {
            Assert.Contains(ActionCatalog.Flatten(restored), a => a.Id == builtin.Id);
        }
    }

    [Fact]
    public void Restore_GroupsIntoSameNamedSubmenu()
    {
        var open = BuiltinActions.Default().Actions
            .OfType<ActionSubmenu>().Single(s => s.Name == "Abrir");
        var user = new ActionConfig(
            [new ActionSubmenu("Abrir",
            [
                new CommandAction("mine-open", "Mine", "myopen", null,
                    [ItemKind.File], ActionOutput.Ignore, [])
            ])],
            new Dictionary<ItemKind, string>());

        var restored = ActionsStore.Restore(user);
        var merged = restored.Actions.OfType<ActionSubmenu>().Single(s => s.Name == "Abrir");

        Assert.Equal("mine-open", ((CommandAction)merged.Actions[0]).Id);
        Assert.Contains(merged.Actions.OfType<CommandAction>(),
            a => a.Id == BuiltinActions.OpenWithDefault);
    }

    [Fact]
    public void Restore_NothingMissing_ReturnsEquivalentConfig()
    {
        var current = BuiltinActions.Default();

        var restored = ActionsStore.Restore(current);

        Assert.Equal(ActionsJson.Serialize(current), ActionsJson.Serialize(restored));
        Assert.Equal(0, BuiltinActions.CountDifference(current, restored));
    }

    [Fact]
    public void Reset_ReturnsIntegralDefaults()
    {
        var user = new ActionConfig([], new Dictionary<ItemKind, string>());

        Assert.Equal(
            ActionsJson.Serialize(BuiltinActions.Default()),
            ActionsJson.Serialize(ActionsStore.Reset()));
        Assert.NotEqual(
            ActionsJson.Serialize(user),
            ActionsJson.Serialize(ActionsStore.Reset()));
    }

    [Fact]
    public void Save_WithBackup_KeepsPreviousConfig()
    {
        ActionsStore.Save(PathFor(), BuiltinActions.Default());
        var changed = BuiltinActions.Default() with
        {
            Defaults = new Dictionary<ItemKind, string>(),
        };

        ActionsStore.Save(PathFor(), changed, backup: true);

        Assert.True(File.Exists(PathFor() + "~"));
        Assert.Equal(
            ActionsJson.Serialize(BuiltinActions.Default()),
            ActionsJson.Serialize(ActionsJson.Deserialize(File.ReadAllText(PathFor() + "~"))));
    }

    [Fact]
    public void CountDifference_CountsIdsMissingFromFirst()
    {
        var user = new ActionConfig([], new Dictionary<ItemKind, string>());

        Assert.Equal(
            ActionCatalog.Flatten(BuiltinActions.Default()).Count(),
            BuiltinActions.CountDifference(user, BuiltinActions.Default()));
        Assert.Equal(
            0,
            BuiltinActions.CountDifference(BuiltinActions.Default(), user));
    }
}
