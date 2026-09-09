// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Shortcut screens cover the persisted map and the hardcoded popup map.
public sealed class ShortcutSettingsTests
{
    [Fact]
    public void PersistedDefaults_MatchPortedShortcuts()
    {
        var shortcuts = new ShortcutSettings();

        Assert.Equal("Ctrl+Shift+V", shortcuts.OpenGesture);
        Assert.Equal("Ctrl+Shift+Alt+V", shortcuts.IncognitoGesture);
        Assert.Equal("Ctrl+S", shortcuts.PinGesture);
        Assert.Equal("Delete", shortcuts.DeleteGesture);
        Assert.Equal("Ctrl+E", shortcuts.EditGesture);
        Assert.Equal("Ctrl+T", shortcuts.EditTitleGesture);
        Assert.Equal("Ctrl+A", shortcuts.OpenMenuGesture);
        Assert.Equal(MiddleClickAction.Pin, shortcuts.MiddleClick);
        Assert.False(shortcuts.SwapCopy);
        Assert.False(shortcuts.SwapScroll);
        Assert.Equal(OpenBehavior.Toggle, shortcuts.OpenBehavior);
    }

    [Fact]
    public void GlobalValidation_RejectsWinF12AndBareKeys()
    {
        Assert.NotNull(ShortcutSettings.ValidateGlobalGesture("Win+V"));
        Assert.NotNull(ShortcutSettings.ValidateGlobalGesture("Ctrl+F12"));
        Assert.NotNull(ShortcutSettings.ValidateGlobalGesture("Delete"));
        Assert.Null(ShortcutSettings.ValidateGlobalGesture("Ctrl+Shift+V"));
        Assert.Null(ShortcutSettings.ValidateGlobalGesture("Ctrl+Shift+Alt+V"));
    }

    [Fact]
    public void LocalValidation_AllowsBareDelete_RejectsGarbage()
    {
        Assert.Null(ShortcutSettings.ValidateLocalGesture("Delete"));
        Assert.Null(ShortcutSettings.ValidateLocalGesture("Ctrl+S"));
        Assert.NotNull(ShortcutSettings.ValidateLocalGesture("Ctrl+Nope"));
        Assert.NotNull(ShortcutSettings.ValidateLocalGesture(""));
    }

    [Fact]
    public void Catalog_PersistedCoversElevenRows_HardcodedCoversPopup()
    {
        var persisted = ShortcutCatalog.Persisted(new ShortcutSettings());
        var hardcoded = ShortcutCatalog.Hardcoded();

        Assert.Equal(11, persisted.Count);
        Assert.All(persisted, row => Assert.True(row.Customizable));
        Assert.Contains(persisted, row => row.Action.Contains("Open history"));
        Assert.Contains(persisted, row => row.Action.Contains("Middle-click"));
        Assert.Contains(hardcoded, row => row.Gesture.Contains("Alt+P"));
        Assert.Contains(hardcoded, row => row.Gesture.Contains("Ctrl+Enter"));
        Assert.Contains(hardcoded, row => row.Gesture.Contains("Esc"));
        Assert.All(hardcoded, row => Assert.False(row.Customizable));
    }

    [Fact]
    public void ResetToDefaults_RestoresPortedGestures()
    {
        var shortcuts = new ShortcutSettings
        {
            OpenGesture = "Ctrl+Alt+X",
            MiddleClick = MiddleClickAction.Delete,
            SwapCopy = true,
        };
        shortcuts.ResetToDefaults();

        Assert.Equal("Ctrl+Shift+V", shortcuts.OpenGesture);
        Assert.Equal(MiddleClickAction.Pin, shortcuts.MiddleClick);
        Assert.False(shortcuts.SwapCopy);
    }
}
