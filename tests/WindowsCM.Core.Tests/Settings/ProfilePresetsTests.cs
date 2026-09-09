// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Default and Compact profiles switch the full 13-key preset matrix;
// first run is Default; any drift reports Custom.
public sealed class ProfilePresetsTests
{
    [Fact]
    public void FirstRun_IsDefault()
    {
        Assert.Equal(ProfileName.Default, ProfilePresets.FirstRun);
        Assert.Equal(ProfileName.Default, AppSettings.Default().DetectProfile());
    }

    [Fact]
    public void DefaultPreset_MatchesProfilesTsLiterals()
    {
        var settings = AppSettings.Default();
        settings.ApplyProfile(ProfileName.Compact);
        settings.ApplyProfile(ProfileName.Default);

        Assert.False(settings.Dialog.ShowAtPointer);
        Assert.Equal(DialogOrientation.Horizontal, settings.Dialog.Orientation);
        Assert.Equal(VerticalDock.Top, settings.Dialog.VerticalPosition);
        Assert.Equal(HorizontalDock.Fill, settings.Dialog.HorizontalPosition);
        Assert.Equal(500, settings.Dialog.Size);
        Assert.False(settings.Dialog.AutoHideSearch);
        Assert.Equal(250, settings.Item.Width);
        Assert.Equal(170, settings.Item.Height);
        Assert.False(settings.Item.DynamicHeight);
        Assert.True(settings.Header.ShowHeader);
        Assert.Equal(HeaderControlsVisibility.Visible, settings.Header.Controls);
        Assert.Equal(FilePreviewVisibility.PreviewOrInfo, settings.PerType.File.Visibility);
        Assert.Equal(DialogOrientation.Vertical, settings.PerType.Link.Orientation);
        Assert.Equal(ProfileName.Default, settings.DetectProfile());
    }

    [Fact]
    public void CompactPreset_MatchesProfilesTsLiterals()
    {
        var settings = AppSettings.Default();
        settings.ApplyProfile(ProfileName.Compact);

        Assert.True(settings.Dialog.ShowAtPointer);
        Assert.Equal(DialogOrientation.Vertical, settings.Dialog.Orientation);
        Assert.Equal(VerticalDock.Fill, settings.Dialog.VerticalPosition);
        Assert.Equal(HorizontalDock.Left, settings.Dialog.HorizontalPosition);
        Assert.Equal(500, settings.Dialog.Size);
        Assert.True(settings.Dialog.AutoHideSearch);
        Assert.Equal(300, settings.Item.Width);
        Assert.Equal(100, settings.Item.Height);
        Assert.True(settings.Item.DynamicHeight);
        Assert.False(settings.Header.ShowHeader);
        Assert.Equal(HeaderControlsVisibility.VisibleOnHover, settings.Header.Controls);
        Assert.Equal(FilePreviewVisibility.InfoOnly, settings.PerType.File.Visibility);
        Assert.Equal(DialogOrientation.Horizontal, settings.PerType.Link.Orientation);
        Assert.Equal(ProfileName.Compact, settings.DetectProfile());
    }

    [Fact]
    public void AnyDrift_ReportsCustom()
    {
        var settings = AppSettings.Default();
        settings.Item.Width = 251;

        Assert.Equal(ProfileName.Custom, settings.DetectProfile());

        var compact = AppSettings.Default();
        compact.ApplyProfile(ProfileName.Compact);
        compact.Dialog.Size = 501;

        Assert.Equal(ProfileName.Custom, compact.DetectProfile());
    }

    [Fact]
    public void ApplyCustom_LeavesSettingsUntouched()
    {
        var settings = AppSettings.Default();
        settings.Item.Width = 251;
        settings.ApplyProfile(ProfileName.Custom);

        Assert.Equal(251, settings.Item.Width);
        Assert.Equal(ProfileName.Custom, settings.DetectProfile());
    }
}
