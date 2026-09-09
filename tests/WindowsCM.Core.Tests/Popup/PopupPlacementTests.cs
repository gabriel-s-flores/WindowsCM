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
}
