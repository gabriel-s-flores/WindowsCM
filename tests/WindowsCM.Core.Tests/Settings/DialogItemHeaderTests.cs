// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Dialog / item / header screens keep every Copyous option with mapped
// defaults and ranges.
public sealed class DialogItemHeaderTests
{
    [Fact]
    public void DialogDefaults_FirstRunDefaultProfile()
    {
        var dialog = new DialogSettings();

        Assert.False(dialog.ShowAtPointer);
        Assert.False(dialog.ShowAtCursor);
        Assert.Equal(DialogOrientation.Horizontal, dialog.Orientation);
        Assert.Equal(VerticalDock.Top, dialog.VerticalPosition);
        Assert.Equal(HorizontalDock.Fill, dialog.HorizontalPosition);
        Assert.Equal(500, dialog.Size);
        Assert.Equal(6, dialog.MarginTop);
        Assert.Equal(6, dialog.MarginRight);
        Assert.Equal(6, dialog.MarginBottom);
        Assert.Equal(6, dialog.MarginLeft);
        Assert.False(dialog.AutoHideSearch);
        Assert.True(dialog.ShowScrollbar);
    }

    [Fact]
    public void DialogClamp_PinsSizeAndMargins()
    {
        var dialog = new DialogSettings { Size = 1, MarginTop = -1, MarginLeft = 99999 };
        dialog.Clamp();

        Assert.Equal(200, dialog.Size);
        Assert.Equal(0, dialog.MarginTop);
        Assert.Equal(10000, dialog.MarginLeft);
    }

    [Fact]
    public void DialogTokens_EachAxisAcceptsOnlyItsOwn()
    {
        Assert.Equal(VerticalDock.Top, DialogSettings.NormalizeVerticalToken("top"));
        Assert.Equal(VerticalDock.Bottom, DialogSettings.NormalizeVerticalToken("bottom"));
        Assert.Equal(HorizontalDock.Left, DialogSettings.NormalizeHorizontalToken("left"));
        Assert.Equal(HorizontalDock.Right, DialogSettings.NormalizeHorizontalToken("right"));

        // Cross-axis tokens mean a corrupt import: they throw instead of
        // silently mapping onto the wrong dock.
        Assert.Throws<FormatException>(() => DialogSettings.NormalizeVerticalToken("left"));
        Assert.Throws<FormatException>(() => DialogSettings.NormalizeVerticalToken("right"));
        Assert.Throws<FormatException>(() => DialogSettings.NormalizeHorizontalToken("top"));
        Assert.Throws<FormatException>(() => DialogSettings.NormalizeHorizontalToken("bottom"));
    }

    [Fact]
    public void ItemDefaults_MatchGschema()
    {
        var item = new ItemSettings();

        Assert.Equal(250, item.Width);
        Assert.Equal(170, item.Height);
        Assert.False(item.DynamicHeight);
        Assert.Equal(4, item.TabWidth);
    }

    [Fact]
    public void ItemClamp_PinsWidthHeightTab()
    {
        var item = new ItemSettings { Width = 1, Height = 9999, TabWidth = 99 };
        item.Clamp();

        Assert.Equal(200, item.Width);
        Assert.Equal(1000, item.Height);
        Assert.Equal(8, item.TabWidth);
    }

    [Fact]
    public void DynamicHeight_AppliesOnlyVertically()
    {
        var item = new ItemSettings { DynamicHeight = true };

        Assert.True(item.DynamicApplies(DialogOrientation.Vertical));
        Assert.False(item.DynamicApplies(DialogOrientation.Horizontal));
        Assert.False(new ItemSettings().DynamicApplies(DialogOrientation.Vertical));
    }

    [Fact]
    public void HeaderDefaults_ShowWithVisibleControlsAndTitle()
    {
        var header = new HeaderSettings();

        Assert.True(header.ShowHeader);
        Assert.Equal(HeaderControlsVisibility.Visible, header.Controls);
        Assert.True(header.ShowItemTitle);
    }
}
