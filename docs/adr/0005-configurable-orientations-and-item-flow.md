# 5. Configurable Clipboard Orientation, Screen Placement and Item Flow

Date: 2026-09-13

## Context

Originally, WindowsCM implemented the large window (`PopupWindow`) as an exclusively horizontal strip anchored at the bottom of the screen (`Bottom`), with items always ordered from left to right (most recent on the left). The compact menu (`CompactPopupWindow`) was introduced as a narrow vertical popup under the mouse cursor (320x480px), always showing the most recent items from top to bottom.

Although this default behavior serves most workflows, several users require:
1. **Large vertical view**: on ultrawide or vertical monitors (code/documents), a left or right sidebar makes better use of the vertical space without covering the lines being worked on at the bottom.
2. **Switching the screen placement**: anchoring at the top of the screen for setups with the taskbar at the top, and choosing the side (left vs right) for right-handed or left-handed users.
3. **Temporal flow control**: some users prefer the natural stack where new items arrive on the right or at the bottom (bottom-to-top), while others prefer the Western reading convention (left-to-right or top-to-bottom).
4. **Compact horizontal layout**: agile users who want a light menu under the cursor, but in a compact horizontal cards format.

## Decision

1. **Separation of Policies and Pure Modules (`WindowsCM.Core.Popup`):**
   - Creation of `ItemOrderingPolicy`: a pure, testable module responsible for reversing the temporal sequence when the active ordering requires recent items at the end (`RecentOnRight` or `RecentOnBottom`), and for deterministically determining the initial selection index.
   - Expansion of `PopupPlacement`: pure functions `PlaceLargePopup` and `PlaceCompactPopup` covering the calculation of screen coordinates with margins and clamping across multiple DPI-aware monitors.
   - Explicit enums in the Core (`LargeHorizontalPosition`, `LargeVerticalPosition`, `HorizontalItemOrder`, `VerticalItemOrder`).

2. **Adaptability of the Large Window (`PopupWindow`):**
   - Horizontal Mode: keeps a full-screen width (`Fill`), a fixed height (348px) and a 3-column top bar, anchoring to `Bottom` or `Top`.
   - Vertical Mode: uses a compact width (~380px), a height filling the work area of the screen (`Fill`) and a top bar adapted to a narrow column, anchoring to `Left` or `Right`.
   - Mouse scrolling and keyboard navigation adapt transparently to the active orientation.

3. **Flexibility of the Compact Menu (`CompactPopupWindow`):**
   - Support for `CompactOrientation.Vertical` (320x480) and `CompactOrientation.Horizontal` (540x240) under the mouse cursor.
   - Support for flow reversal (bottom to top when vertical, right to left when horizontal).

4. **Interactive Visual Experience in the Settings Panel:**
   - Addition of the "Layout & Placement" tab to `SettingsWindow`, with a stylized monitor and desktop frame.
   - Animated, real-time preview with mocked smoke test data, visually showing where the clipboard will be placed and in which direction the items will flow before applying.

## Consequences

- **Positive:**
  - Full ergonomic freedom for any monitor profile and usage preference.
  - The Core stays 100% pure and testable, with no dependencies on the WPF visual pipeline.
  - Full backward compatibility: previous settings keep the current default values.
- **Negative / Challenges:**
  - `PopupWindow` requires a responsive adaptation of the header when switching between the wide horizontal layout and the narrow vertical one.
