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
        // PopupWindow.xaml Width="880" (horizontal card layout).
        Assert.Equal(880, PopupSizing.FixedWidth);
    }

    [Fact]
    public void MaxHeight_MatchesXamlWindow()
    {
        // PopupWindow.xaml MaxHeight="320".
        Assert.Equal(320, PopupSizing.MaxHeight);
    }

    [Fact]
    public void ClampHeight_PassesThroughBelowMax()
    {
        Assert.Equal(200, PopupSizing.ClampHeight(200));
    }

    [Fact]
    public void ClampHeight_ClampsAtMax()
    {
        Assert.Equal(320, PopupSizing.ClampHeight(900));
    }

    [Fact]
    public void ClampHeight_DeterministicForSameContent()
    {
        Assert.Equal(PopupSizing.ClampHeight(280), PopupSizing.ClampHeight(280));
    }
}
