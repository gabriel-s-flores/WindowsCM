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
    public void SameCursor_SameContent_SamePosition()
    {
        var first = PopupPlacement.PlaceAtCursor(960, 540, 380, 300, Area1080p);
        var second = PopupPlacement.PlaceAtCursor(960, 540, 380, 300, Area1080p);

        Assert.Equal(first, second);
    }
}
