// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

// Prototype verdict pins (issue 07 → spec): winner A (Cards) at Copyous
// density, Dark default, no vertical dead space, full-width rows.
public sealed class PopupThemeTests
{
    [Fact]
    public void Verdict_CardsAtCopyousDensity()
    {
        Assert.Equal(250, PopupCards.CardWidth);
        Assert.Equal(170, PopupCards.CardHeight);
    }

    [Fact]
    public void Verdict_DarkDefault_FirstRunDefaultProfile()
    {
        Assert.Equal(PopupTheme.Dark, PopupCards.DefaultTheme);
        Assert.Equal(PopupProfile.Default, PopupCards.FirstRunProfile);
    }

    [Fact]
    public void Verdict_NoDeadSpace_FullWidth()
    {
        Assert.True(PopupCards.NoVerticalDeadSpace);
        Assert.True(PopupCards.FullWidthRows);
    }

    [Fact]
    public void Tags_NineCopyousColors()
    {
        Assert.Equal(9, ItemTags.All.Count);
        Assert.Equal("#3584e4", ItemTags.All[0]);
        Assert.Equal("#6f8396", ItemTags.All[^1]);
        Assert.Equal(9, ItemTags.All.Distinct().Count());
    }
}
