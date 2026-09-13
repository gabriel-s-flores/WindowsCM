// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Feedback;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Aggregate root: first-run defaults, clamping, runtime bridges,
// and JSON persistence.
public sealed class AppSettingsTests
{
    [Fact]
    public void Default_IsFirstRunDefaultProfile()
    {
        var settings = AppSettings.Default();

        Assert.Equal(ProfileName.Default, settings.DetectProfile());
        Assert.Equal(DialogOrientation.Horizontal, settings.Dialog.Orientation);
        Assert.Equal(ThemeChoice.Default, settings.Theme.Theme);
    }

    [Fact]
    public void ClampAll_PinsEveryRange()
    {
        var settings = AppSettings.Default();
        settings.History.MaxItems = 1;
        settings.Item.Width = 1;
        settings.Dialog.Size = 1;
        settings.PerType.Character.MaxCharacters = 99;
        settings.Feedback.VolumeDb = 99.0;
        settings.Paste.DelayMs = 1;

        settings.ClampAll();

        Assert.Equal(10, settings.History.MaxItems);
        Assert.Equal(200, settings.Item.Width);
        Assert.Equal(200, settings.Dialog.Size);
        Assert.Equal(4, settings.PerType.Character.MaxCharacters);
        Assert.Equal(0.0, settings.Feedback.VolumeDb);
        Assert.Equal(100, settings.Paste.DelayMs);
    }

    [Fact]
    public void ToCaptureOptions_BridgesExclusionsMaxCharsAndDatePolicy()
    {
        var settings = AppSettings.Default();
        settings.Exclusions.Add("notepad.exe");
        settings.PerType.Character.MaxCharacters = 3;
        settings.Behavior.UpdateDateOnCopy = false;

        CaptureOptions capture = settings.ToCaptureOptions();

        Assert.Contains("notepad.exe", capture.ExcludedProcesses);
        Assert.Equal(3, capture.MaxCharacters);
        Assert.False(capture.UpdateDateOnCopy);
    }

    [Fact]
    public void ToPasteOptions_CombinesSequenceDelayAndSwap()
    {
        var settings = AppSettings.Default();
        settings.Paste.Sequence = PasteSequence.ShiftInsert;
        settings.Shortcuts.SwapCopy = true;

        var paste = settings.ToPasteOptions();

        Assert.Equal(PasteSequence.ShiftInsert, paste.PasteSequence);
        Assert.True(paste.SwapCopyPaste);
    }

    [Fact]
    public void ToFeedbackOptions_PreserveToggles()
    {
        var settings = AppSettings.Default();
        settings.Feedback.Sound = SoundName.Bell;

        Assert.Equal(SoundName.Bell, settings.ToSoundOptions().Name);
        Assert.True(settings.ToCopyFeedbackOptions().WiggleIndicator);
    }

    [Fact]
    public void SettingsStore_RoundTripsThroughJson()
    {
        var settings = AppSettings.Default();
        settings.History.MaxItems = 85;
        settings.Dialog.Orientation = DialogOrientation.Vertical;
        settings.Theme.Theme = ThemeChoice.Custom;
        settings.Theme.CustomBg = "rgb(1,2,3)";
        settings.Exclusions.Add("notepad.exe");
        settings.Shortcuts.SwapScroll = true;

        var restored = SettingsStore.Deserialize(SettingsStore.Serialize(settings));

        Assert.Equal(85, restored.History.MaxItems);
        Assert.Equal(DialogOrientation.Vertical, restored.Dialog.Orientation);
        Assert.Equal(ThemeChoice.Custom, restored.Theme.Theme);
        Assert.Equal("rgb(1,2,3)", restored.Theme.CustomBg);
        Assert.Contains("notepad.exe", restored.Exclusions.Processes);
        Assert.True(restored.Shortcuts.SwapScroll);
    }

    [Fact]
    public void SettingsStore_NullSections_DegradeToDefaults()
    {
        var restored = SettingsStore.Deserialize(
            """{"history":null,"dialog":null,"perType":{"file":null,"link":null},"theme":null}""");

        Assert.Equal(ProfileName.Default, restored.DetectProfile());
        Assert.Equal(100, restored.History.MaxItems);
        Assert.NotNull(restored.Dialog);
        Assert.NotNull(restored.PerType.File);
        Assert.NotNull(restored.PerType.Link);
        Assert.NotNull(restored.Theme);
    }

    [Fact]
    public void SettingsStore_MissingFile_ReturnsDefaultsWithoutCreating()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");

        var settings = SettingsStore.Load(path);

        Assert.Equal(ProfileName.Default, settings.DetectProfile());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SettingsStore_CorruptFile_DegradesToDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, "{ not json");

        var settings = SettingsStore.Load(path);

        Assert.Equal(ProfileName.Default, settings.DetectProfile());
    }

    [Fact]
    public void SettingsStore_SaveIsAtomicWithOptionalBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        var first = AppSettings.Default();
        SettingsStore.Save(path, first);

        var second = AppSettings.Default();
        second.History.MaxItems = 75;
        SettingsStore.Save(path, second, backup: true);

        Assert.True(File.Exists(path + "~"));
        Assert.Equal(75, SettingsStore.Load(path).History.MaxItems);
        Assert.False(Directory.EnumerateFiles(dir, "*.tmp").Any());
    }

    [Fact]
    public void SettingsStore_LoadClampsOutOfRange()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{"history":{"maxItems":9999},"item":{"width":1}}""");

        var settings = SettingsStore.Load(path);

        Assert.Equal(100, settings.History.MaxItems);
        Assert.Equal(200, settings.Item.Width);
    }

    [Fact]
    public void DialogSettings_ScrollbarPosition_DefaultsAndClamping()
    {
        var settings = AppSettings.Default();
        Assert.Equal(VerticalScrollbarPosition.Right, settings.Dialog.VerticalScrollbarPosition);
        Assert.Equal(HorizontalScrollbarPosition.Bottom, settings.Dialog.HorizontalScrollbarPosition);

        // Invalid enum cast
        settings.Dialog.VerticalScrollbarPosition = (VerticalScrollbarPosition)999;
        settings.Dialog.HorizontalScrollbarPosition = (HorizontalScrollbarPosition)999;
        settings.Dialog.Clamp();

        Assert.Equal(VerticalScrollbarPosition.Right, settings.Dialog.VerticalScrollbarPosition);
        Assert.Equal(HorizontalScrollbarPosition.Bottom, settings.Dialog.HorizontalScrollbarPosition);

        // Valid custom values
        settings.Dialog.VerticalScrollbarPosition = VerticalScrollbarPosition.Left;
        settings.Dialog.HorizontalScrollbarPosition = HorizontalScrollbarPosition.Top;
        settings.Dialog.Clamp();

        Assert.Equal(VerticalScrollbarPosition.Left, settings.Dialog.VerticalScrollbarPosition);
        Assert.Equal(HorizontalScrollbarPosition.Top, settings.Dialog.HorizontalScrollbarPosition);
    }

    [Fact]
    public void SettingsStore_ScrollbarPosition_RoundtripsThroughJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");

        var original = AppSettings.Default();
        original.Dialog.VerticalScrollbarPosition = VerticalScrollbarPosition.Left;
        original.Dialog.HorizontalScrollbarPosition = HorizontalScrollbarPosition.Top;
        SettingsStore.Save(path, original);

        var loaded = SettingsStore.Load(path);
        Assert.Equal(VerticalScrollbarPosition.Left, loaded.Dialog.VerticalScrollbarPosition);
        Assert.Equal(HorizontalScrollbarPosition.Top, loaded.Dialog.HorizontalScrollbarPosition);
    }
}
