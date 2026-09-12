// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class PopupPlacementTests
{
    private static readonly WorkArea Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void Open_OffsetsFromCursor()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(100, 200, 400, 300, Screen);

        Assert.Equal(112, left);
        Assert.Equal(212, top);
    }

    [Fact]
    public void RightEdge_ClampsInsideWorkArea()
    {
        var (left, _) = PopupPlacement.PlaceAtCursor(1800, 100, 400, 300, Screen);

        Assert.Equal(1520, left);
    }

    [Fact]
    public void BottomEdge_ClampsInsideWorkArea()
    {
        var (_, top) = PopupPlacement.PlaceAtCursor(100, 1000, 400, 300, Screen);

        Assert.Equal(740, top);
    }

    [Fact]
    public void SecondaryMonitor_NonZeroOrigin_ClampsToItsArea()
    {
        var right = new WorkArea(1920, 0, 3840, 1040);

        var (left, top) = PopupPlacement.PlaceAtCursor(3700, 900, 400, 300, right);

        Assert.Equal(3440, left);
        Assert.Equal(740, top);
        Assert.True(left >= right.Left);
        Assert.True(top >= right.Top);
    }

    [Fact]
    public void PopupLargerThanArea_PinsToOrigin()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(100, 100, 4000, 3000, Screen);

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void ToDips_ScalesPhysicalPixels()
    {
        // 150% DPI: 150px -> 100 DIPs (TransformFromDevice parity).
        Assert.Equal(100, PopupPlacement.ToDips(150, 2.0 / 3.0), precision: 9);
    }

    // Ticket 21: deterministic opens on 1920x1080 @100%. Work area is the
    // real 1080p geometry minus the taskbar (1920x1032 observed); the popup
    // uses the production card-strip size (380 wide, up to 520 tall) with
    // the fixed +12 cursor offset, clamped inside the work area.
    private static readonly WorkArea Area1080p = new(0, 0, 1920, 1032);

    [Fact]
    public void Center1080p_SmallPopup_OffsetsByTwelve()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(960, 540, 380, 300, Area1080p);

        Assert.Equal(972, left);
        Assert.Equal(552, top);
    }

    [Fact]
    public void TopLeft1080p_FullPopup_OffsetsByTwelve()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(10, 10, 380, 520, Area1080p);

        Assert.Equal(22, left);
        Assert.Equal(22, top);
    }

    [Fact]
    public void TopRight1080p_FullPopup_ClampsLeft()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(1900, 10, 380, 520, Area1080p);

        Assert.Equal(1540, left);
        Assert.Equal(22, top);
    }

    [Fact]
    public void BottomLeft1080p_FullPopup_ClampsTop()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(10, 1000, 380, 520, Area1080p);

        Assert.Equal(22, left);
        Assert.Equal(512, top);
    }

    [Fact]
    public void BottomRight1080p_FullPopup_ClampsBoth()
    {
        var (left, top) = PopupPlacement.PlaceAtCursor(1900, 1000, 380, 520, Area1080p);

        Assert.Equal(1540, left);
        Assert.Equal(512, top);
    }

    [Fact]
    public void HorizontalLayout1080p_ClampsWithinWorkArea()
    {
        // 880x320 horizontal popup
        var (left, top) = PopupPlacement.PlaceAtCursor(1800, 950, 880, 320, Area1080p);

        // Right clamp: 1920 - 880 = 1040
        Assert.Equal(1040, left);
        // Bottom clamp: 1032 - 320 = 712
        Assert.Equal(712, top);
    }

    [Fact]
    public void SameCursor_SameContent_SamePosition()
    {
        var first = PopupPlacement.PlaceAtCursor(960, 540, 380, 300, Area1080p);
        var second = PopupPlacement.PlaceAtCursor(960, 540, 380, 300, Area1080p);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PlaceHorizontalFill_SpansScreenWidthWithMargins_ClampsTop()
    {
        var (left, top, width) = PopupPlacement.PlaceHorizontalFill(500, 320, Area1080p, margin: 20);

        Assert.Equal(20, left);
        Assert.Equal(1880, width);
        Assert.Equal(512, top);
    }

    [Fact]
    public void PlaceHorizontalFill_NearBottom_ClampsToWorkAreaBottom()
    {
        var (left, top, width) = PopupPlacement.PlaceHorizontalFill(1000, 320, Area1080p, margin: 20);

        Assert.Equal(20, left);
        Assert.Equal(1880, width);
        Assert.Equal(712, top);
    }

    [Fact]
    public void CalculateHorizontalFillWidth_EnforcesMinWidth()
    {
        var smallScreen = new WorkArea(0, 0, 500, 400);
        var width = PopupPlacement.CalculateHorizontalFillWidth(smallScreen, margin: 20);

        Assert.Equal(PopupSizing.MinWidth, width);
    }

    [Fact]
    public void PopupSizing_MaxHeight_ProvidesScrollbarClearance()
    {
        // Ticket 29: 240px card + 44px header + 24px padding + 40px scrollbar and breathing room = 348px
        Assert.Equal(348, PopupSizing.MaxHeight);
        Assert.Equal(348, PopupSizing.ClampHeight(400));
        Assert.Equal(300, PopupSizing.ClampHeight(300));
    }

    [Fact]
    public void PlaceHorizontalFill_WithMaxHeight_ClampsToWorkAreaBottom()
    {
        var (left, top, width) = PopupPlacement.PlaceHorizontalFill(1000, PopupSizing.MaxHeight, Area1080p, margin: 20);

        Assert.Equal(20, left);
        Assert.Equal(1880, width);
        // 1032 - 348 = 684
        Assert.Equal(684, top);
    }
}
