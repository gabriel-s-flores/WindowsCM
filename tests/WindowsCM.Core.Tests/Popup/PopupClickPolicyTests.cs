// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;
using Xunit;

namespace WindowsCM.Core.Tests.Popup;

public sealed class PopupClickPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(42)]
    public void ShouldActivate_WhenVisibleAndValidIndex_ReturnsTrue(int index)
    {
        Assert.True(PopupClickPolicy.ShouldActivate(isVisible: true, clickedIndex: index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ShouldActivate_WhenNotVisible_ReturnsFalse_EvenWithValidIndex(int index)
    {
        // Guards the trailing click of a double-click: the first click hid the
        // popup, so the second click sees !visible and becomes an idempotent no-op.
        Assert.False(PopupClickPolicy.ShouldActivate(isVisible: false, clickedIndex: index));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldActivate_WhenNullIndex_ReturnsFalse(bool isVisible)
    {
        // Empty area, scrollbar, header or margins hit-test outside any ListBoxItem.
        Assert.False(PopupClickPolicy.ShouldActivate(isVisible, clickedIndex: null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void ShouldActivate_WhenNegativeIndex_ReturnsFalse(int negativeIndex)
    {
        // Container generator returns -1 for unmapped containers.
        Assert.False(PopupClickPolicy.ShouldActivate(isVisible: true, clickedIndex: negativeIndex));
        Assert.False(PopupClickPolicy.ShouldActivate(isVisible: false, clickedIndex: negativeIndex));
    }
}
