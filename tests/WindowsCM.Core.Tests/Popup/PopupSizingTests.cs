// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

// Ticket 21: stable popup size on 1080p — fixed width every open for the
// same history, clamped height, no search-filter resize. Pure contract
// behind PopupWindow (no UI automation in v1 per Testing Decisions).
public sealed class PopupSizingTests
{
    [Fact]
    public void FixedWidth_MatchesXamlCardStrip()
    {
        // PopupWindow.xaml Width="380" — the shell enforces this constant
        // so right-edge clamping never drifts with measured content width.
        Assert.Equal(380, PopupSizing.FixedWidth);
    }

    [Fact]
    public void MaxHeight_MatchesXamlWindow()
    {
        // PopupWindow.xaml MaxHeight="520".
        Assert.Equal(520, PopupSizing.MaxHeight);
    }

    [Fact]
    public void ClampHeight_PassesThroughBelowMax()
    {
        Assert.Equal(300, PopupSizing.ClampHeight(300));
    }

    [Fact]
    public void ClampHeight_ClampsAtMax()
    {
        Assert.Equal(520, PopupSizing.ClampHeight(900));
    }

    [Fact]
    public void ClampHeight_DeterministicForSameContent()
    {
        Assert.Equal(PopupSizing.ClampHeight(412), PopupSizing.ClampHeight(412));
    }
}
