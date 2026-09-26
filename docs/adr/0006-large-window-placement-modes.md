# 6. Large window placement modes: fixed monitor and free

Date: 2026-09-25

## Context

The large window (`PopupWindow`) could only be docked to an edge (top/bottom when horizontal, left/right when vertical, ADR 0005) of the monitor the mouse was on. On desks with several monitors the user wants it to always open on the same monitor, regardless of where the mouse is; and some prefer an undocked window, positioned and sized by hand.

"A mode for multiple monitors" was decided as **picking a fixed monitor** (not stretching the window across all screens, nor following the active window). Free mode allows **dragging and resizing**.

## Decision

1. **`DialogSettings.LargePlacement`** (`FollowMouse` as the default, `FixedMonitor`, `Free`). The two docked modes keep using `LargeHorizontalPosition` / `LargeVerticalPosition`; in free mode the edges do not apply.
2. **Fixed monitor by `Screen.DeviceName`** (`LargeMonitor`, empty = primary). It is the identity Windows keeps across sessions; an index or coordinates would change when the screens are rearranged. While the monitor is disconnected, the primary takes over, but the choice stays saved and applies again when it reappears (`MonitorLayout.Resolve`).
3. **Numbering in desk order** (left→right, then top→bottom, `MonitorLayout.Ordered`) in the Settings list and in the **Identify** overlay, which shows the number in the center of each monitor for 2 s (without capturing focus or clicks).
4. **Free mode with one rectangle per orientation** (`LargeFreeBoundsHorizontal` / `LargeFreeBoundsVertical`, in WPF DIPs — the same space as `Window.Left/Top`). A wide strip and a tall column are different shapes; switching the orientation does not squeeze one into the other. With no saved rectangle, the window opens floating in the center of the monitor under the mouse.
5. **Never off-screen** (`PopupPlacement.PlaceFree`): the saved rectangle is pulled entirely into the working area it overlaps the most (and shrunk if the monitor got smaller); if it overlaps none (monitor removed), it starts over centered. A minimum size per orientation ensures the header fits.
6. **Resize by answering `WM_NCHITTEST`** (`ResizeHitTest.EdgeAt` → `HTLEFT`…`HTBOTTOMRIGHT`) only in free mode, letting Windows drive the native resize loop of the borderless window; **drag with `DragMove`** from the handle at the top and from any area that is not a control. `WM_EXITSIZEMOVE` saves the rectangle on release. `WindowChrome` is not used, so that the window does not change in the docked modes.

## Consequences

- **Positive:** the monitor and free-position rules are pure and tested in Core; old settings load `FollowMouse` (the previous behavior); nothing changes for anyone who does not touch the option.
- **Negative / challenges:** validating drag/resize and the overlay is manual (smoke on Windows); accuracy with different DPI scales across monitors depends on the process's DPI mode (WPF without a manifest runs "system aware", in which DIPs are consistent across screens).
