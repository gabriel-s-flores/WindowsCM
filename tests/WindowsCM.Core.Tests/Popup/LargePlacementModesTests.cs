// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Popup;

public sealed class LargePlacementModesTests
{
    private static readonly WorkArea LeftScreen = new(0, 0, 1920, 1040);
    private static readonly WorkArea RightScreen = new(1920, 0, 4480, 1400);

    private static MonitorArea Monitor(string name, WorkArea area, bool primary = false) =>
        new(name, area, area, primary);

    [Fact]
    public void Ordered_NumbersMonitorsLeftToRightThenTopToBottom()
    {
        var top = Monitor(@"\\.\DISPLAY3", new WorkArea(0, -1080, 1920, 0));
        var right = Monitor(@"\\.\DISPLAY1", RightScreen, primary: true);
        var left = Monitor(@"\\.\DISPLAY2", LeftScreen);

        var ordered = MonitorLayout.Ordered([right, left, top]);

        Assert.Equal([@"\\.\DISPLAY3", @"\\.\DISPLAY2", @"\\.\DISPLAY1"], ordered.Select(m => m.DeviceName));
    }

    [Fact]
    public void Resolve_PicksTheSavedMonitor_IgnoringCase()
    {
        var monitors = new[] { Monitor(@"\\.\DISPLAY1", LeftScreen, primary: true), Monitor(@"\\.\DISPLAY2", RightScreen) };

        Assert.Equal(RightScreen, MonitorLayout.Resolve(@"\\.\display2", monitors)?.WorkingArea);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(@"\\.\DISPLAY9")]
    public void Resolve_EmptyOrDisconnected_FallsBackToPrimary(string? saved)
    {
        var monitors = new[] { Monitor(@"\\.\DISPLAY2", RightScreen), Monitor(@"\\.\DISPLAY1", LeftScreen, primary: true) };

        Assert.Equal(@"\\.\DISPLAY1", MonitorLayout.Resolve(saved, monitors)?.DeviceName);
    }

    [Fact]
    public void Resolve_NoMonitors_IsNull()
    {
        Assert.Null(MonitorLayout.Resolve(@"\\.\DISPLAY1", []));
    }

    [Fact]
    public void PlaceFree_ReopensExactlyWhereTheUserLeftIt()
    {
        var saved = new WindowBounds { Left = 2200, Top = 300, Width = 900, Height = 400 };

        var placed = PopupPlacement.PlaceFree(saved, [LeftScreen, RightScreen], LeftScreen, DialogOrientation.Horizontal);

        Assert.Equal((2200d, 300d, 900d, 400d), placed);
    }

    [Fact]
    public void PlaceFree_PartlyOffScreen_IsPulledBackInside()
    {
        var saved = new WindowBounds { Left = 1500, Top = 900, Width = 900, Height = 400 };

        var (left, top, width, height) = PopupPlacement.PlaceFree(saved, [LeftScreen], LeftScreen, DialogOrientation.Horizontal);

        Assert.Equal(1020, left);
        Assert.Equal(640, top);
        Assert.Equal(900, width);
        Assert.Equal(400, height);
    }

    [Fact]
    public void PlaceFree_MonitorGone_StartsOverCenteredOnTheFallback()
    {
        // Saved on the right monitor, which is now disconnected.
        var saved = new WindowBounds { Left = 2200, Top = 300, Width = 900, Height = 400 };

        var (left, top, width, height) = PopupPlacement.PlaceFree(saved, [LeftScreen], LeftScreen, DialogOrientation.Horizontal);

        Assert.True(left >= LeftScreen.Left && left + width <= LeftScreen.Right);
        Assert.True(top >= LeftScreen.Top && top + height <= LeftScreen.Bottom);
        Assert.Equal((LeftScreen.Right - width) / 2, left, 3);
    }

    [Fact]
    public void PlaceFree_LargerThanItsMonitorNow_ShrinksToFit()
    {
        var saved = new WindowBounds { Left = 0, Top = 0, Width = 3000, Height = 2000 };

        var placed = PopupPlacement.PlaceFree(saved, [LeftScreen], LeftScreen, DialogOrientation.Vertical);

        Assert.Equal((0d, 0d, 1920d, 1040d), placed);
    }

    [Fact]
    public void PlaceFree_TooSmall_GrowsToTheMinimum()
    {
        var saved = new WindowBounds { Left = 100, Top = 100, Width = 50, Height = 50 };

        var (_, _, width, height) = PopupPlacement.PlaceFree(saved, [LeftScreen], LeftScreen, DialogOrientation.Horizontal);

        Assert.Equal(PopupPlacement.FreeMinimumSize(DialogOrientation.Horizontal), (width, height));
    }

    [Theory]
    [InlineData(DialogOrientation.Horizontal)]
    [InlineData(DialogOrientation.Vertical)]
    public void PlaceFree_NothingSaved_FloatsCenteredAndInside(DialogOrientation orientation)
    {
        var (left, top, width, height) = PopupPlacement.PlaceFree(null, [LeftScreen], LeftScreen, orientation);
        var (minWidth, minHeight) = PopupPlacement.FreeMinimumSize(orientation);

        Assert.True(width >= minWidth && height >= minHeight);
        Assert.True(width < LeftScreen.Right && height < LeftScreen.Bottom); // detached from the edges
        Assert.Equal((LeftScreen.Right - width) / 2, left, 3);
        Assert.Equal((LeftScreen.Bottom - height) / 2, top, 3);
    }

    [Fact]
    public void PlaceFree_UnusableSavedBounds_AreIgnored()
    {
        var saved = new WindowBounds { Left = double.NaN, Top = 0, Width = 900, Height = 400 };

        var placed = PopupPlacement.PlaceFree(saved, [LeftScreen], LeftScreen, DialogOrientation.Horizontal);

        Assert.Equal(PopupPlacement.DefaultFreeBounds(DialogOrientation.Horizontal, LeftScreen), placed);
    }

    [Fact]
    public void Settings_DefaultToTheOriginalDockedBehavior()
    {
        var dialog = new DialogSettings();

        Assert.Equal(LargePlacementMode.FollowMouse, dialog.LargePlacement);
        Assert.Equal("", dialog.LargeMonitor);
        Assert.Null(dialog.LargeFreeBoundsHorizontal);
        Assert.Null(dialog.LargeFreeBoundsVertical);
    }

    [Fact]
    public void Settings_KeepOneFreeRectanglePerOrientation()
    {
        var dialog = new DialogSettings();
        var strip = new WindowBounds { Left = 10, Top = 20, Width = 900, Height = 348 };
        var column = new WindowBounds { Left = 30, Top = 40, Width = 380, Height = 800 };

        dialog.SetFreeBounds(DialogOrientation.Horizontal, strip);
        dialog.SetFreeBounds(DialogOrientation.Vertical, column);

        Assert.Same(strip, dialog.FreeBoundsFor(DialogOrientation.Horizontal));
        Assert.Same(column, dialog.FreeBoundsFor(DialogOrientation.Vertical));
    }

    [Fact]
    public void Settings_RoundTripAndClampCorruptValues()
    {
        var settings = AppSettings.Default();
        settings.Dialog.LargePlacement = LargePlacementMode.FixedMonitor;
        settings.Dialog.LargeMonitor = @"\\.\DISPLAY2";
        settings.Dialog.LargeFreeBoundsVertical = new WindowBounds { Left = -1500, Top = 10, Width = 380, Height = 900 };

        var restored = SettingsStore.Deserialize(SettingsStore.Serialize(settings));

        Assert.Equal(LargePlacementMode.FixedMonitor, restored.Dialog.LargePlacement);
        Assert.Equal(@"\\.\DISPLAY2", restored.Dialog.LargeMonitor);
        Assert.Equal(-1500, restored.Dialog.LargeFreeBoundsVertical?.Left);
        Assert.Equal(900, restored.Dialog.LargeFreeBoundsVertical?.Height);

        var corrupt = SettingsStore.Deserialize(
            """{"dialog":{"largePlacement":"Free","largeMonitor":null,"largeFreeBoundsHorizontal":{"left":0,"top":0,"width":0,"height":300}}}""");

        Assert.Equal(LargePlacementMode.Free, corrupt.Dialog.LargePlacement);
        Assert.Equal("", corrupt.Dialog.LargeMonitor);
        Assert.Null(corrupt.Dialog.LargeFreeBoundsHorizontal);
    }

    [Theory]
    [InlineData(500, 300, ResizeEdge.None)]      // inside: the window's own content
    [InlineData(102, 300, ResizeEdge.Left)]
    [InlineData(897, 300, ResizeEdge.Right)]
    [InlineData(500, 202, ResizeEdge.Top)]
    [InlineData(500, 597, ResizeEdge.Bottom)]
    [InlineData(102, 202, ResizeEdge.TopLeft)]
    [InlineData(112, 202, ResizeEdge.TopLeft)]   // corner zone is twice the border
    [InlineData(897, 202, ResizeEdge.TopRight)]
    [InlineData(102, 597, ResizeEdge.BottomLeft)]
    [InlineData(897, 590, ResizeEdge.BottomRight)]
    [InlineData(50, 300, ResizeEdge.None)]       // outside the window
    public void ResizeHitTest_FindsTheGrabbedEdge(double x, double y, ResizeEdge expected)
    {
        var window = new WorkArea(100, 200, 900, 600);

        Assert.Equal(expected, ResizeHitTest.EdgeAt(x, y, window, border: 8));
    }
}
