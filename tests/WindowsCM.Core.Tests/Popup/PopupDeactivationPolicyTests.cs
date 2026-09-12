// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;
using Xunit;

namespace WindowsCM.Core.Tests.Popup;

public sealed class PopupDeactivationPolicyTests
{
    [Fact]
    public void ShouldHide_WhenContextMenuIsOpen_ReturnsFalse()
    {
        // Ticket 29: Opening a context menu deactivates the main window;
        // it must never trigger popup hiding.
        var shouldHide = PopupDeactivationPolicy.ShouldHide(
            isVisible: true,
            elapsedSinceShowMs: 1000,
            isContextMenuOpen: true,
            isDialogOpen: false);

        Assert.False(shouldHide);
    }

    [Fact]
    public void ShouldHide_WhenDialogOpen_ReturnsFalse()
    {
        // Ticket 29: Opening a modal dialog (e.g. edit title/content) must not hide the popup.
        var shouldHide = PopupDeactivationPolicy.ShouldHide(
            isVisible: true,
            elapsedSinceShowMs: 1000,
            isContextMenuOpen: false,
            isDialogOpen: true);

        Assert.False(shouldHide);
    }

    [Fact]
    public void ShouldHide_WhenTooEarly_ReturnsFalse()
    {
        // Ticket 28 & 29: Focus bounce protection within minimum lifetime
        var shouldHide = PopupDeactivationPolicy.ShouldHide(
            isVisible: true,
            elapsedSinceShowMs: 150,
            isContextMenuOpen: false,
            isDialogOpen: false);

        Assert.False(shouldHide);
    }

    [Fact]
    public void ShouldHide_WhenNotVisible_ReturnsFalse()
    {
        var shouldHide = PopupDeactivationPolicy.ShouldHide(
            isVisible: false,
            elapsedSinceShowMs: 1000,
            isContextMenuOpen: false,
            isDialogOpen: false);

        Assert.False(shouldHide);
    }

    [Fact]
    public void ShouldHide_WhenNormalDeactivationAfterLifetime_ReturnsTrue()
    {
        var shouldHide = PopupDeactivationPolicy.ShouldHide(
            isVisible: true,
            elapsedSinceShowMs: 500,
            isContextMenuOpen: false,
            isDialogOpen: false);

        Assert.True(shouldHide);
    }
}
