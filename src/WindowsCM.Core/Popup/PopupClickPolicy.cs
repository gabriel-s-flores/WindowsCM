// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Single-click mouse policy (ticket 23): a left click on a popup row
// activates exactly like Enter (copy-and-paste unless Shift says
// copy-only downstream in the orchestrator); keyboard navigation never
// activates on its own. Pure so the WPF shell stays a thin renderer and
// the rule is unit-pinned without UI automation (spec: no UI automation
// in v1).
//
// - isVisible: the popup must be open (guards the second half of a
//   double-click: the first click hides the popup, so the trailing
//   DoubleClick event sees !visible and becomes a no-op — idempotent).
// - clickedIndex: row under the mouse-up point (null = empty area /
//   scrollbar / header — never activates). Shift does NOT gate this:
//   Shift+click still activates, the orchestrator turns it into CopyOnly
//   (no injection, no hide) while keeping every diagnostic visible.
public static class PopupClickPolicy
{
    public static bool ShouldActivate(bool isVisible, int? clickedIndex) =>
        isVisible && clickedIndex.HasValue && clickedIndex.Value >= 0;
}
