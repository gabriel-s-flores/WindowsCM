// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Pure deactivation policy for popup window.
// Guards against premature hide during context menus, modals, and initial focus transition.
public static class PopupDeactivationPolicy
{
    public const long MinimumLifetimeMs = 250;

    public static bool ShouldHide(
        bool isVisible,
        long elapsedSinceShowMs,
        bool isContextMenuOpen,
        bool isDialogOpen)
    {
        if (!isVisible)
        {
            return false;
        }
        if (isContextMenuOpen || isDialogOpen)
        {
            return false;
        }
        if (elapsedSinceShowMs < MinimumLifetimeMs)
        {
            return false;
        }
        return true;
    }
}
