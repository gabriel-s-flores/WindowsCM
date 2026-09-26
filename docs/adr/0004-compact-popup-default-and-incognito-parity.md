# 4. Compact Menu as the Default for Global Hotkeys and Incognito Mode Parity

Date: 2026-09-13

## Context

WindowsCM's default global hotkey (`Ctrl+Shift+V`) originally opened the full window in the horizontal cards format (880x320px). Although this wide view offers a rich display of code and images, its size ends up taking a considerable portion of the screen during everyday typing, interrupting the workflow of a user who just wants to quickly select and paste a recent item.

Additionally:
1. A separate secondary hotkey (`Ctrl+\``) had been created exclusively for the compact menu, causing unnecessary hotkey fragmentation and potential collisions.
2. The compact menu had no permanent visual search bar at the top, relying on an unintuitive hidden typing-capture mechanism.
3. The private mode icon in the compact menu used a generic glyph (`\uE727`), clashing with the classic vector icon of a Fedora hat and sunglasses adopted in the full view.

## Decision

1. **Compact Menu as the Default Target of the Global Hotkeys:**
   - The main hotkey `Ctrl+Shift+V` now opens the `CompactPopupWindow` directly, placed intelligently under the mouse cursor.
   - The incognito mode hotkey `Ctrl+Shift+Alt+V` now also opens the compact menu under the cursor, activating the ephemeral session and showing clear visual feedback.
   - Complete removal of the standalone `Ctrl+\`` hotkey (`HotkeySlot.Compact`), consolidating the global registrar into the two essential slots: `Open` and `Incognito`.

2. **Search Bar at the Top of the Compact Menu:**
   - Addition of a Fluent search container fixed at the top (`Height="32"`, `CornerRadius="8"`), with a magnifying glass icon, a text box with a friendly placeholder ("Search...") and an instant clear button.
   - Automatic focus on the search bar when the menu opens, with support for direct vertical navigation in the list via the Up/Down arrows and activation with Enter, without having to switch focus manually.

3. **Visual Unification of Incognito Mode:**
   - Application of the same `IncognitoHatAndGlassesGeometry` vector geometry both to the footer button and to the new top alert banner of the compact menu.
   - Active purple highlight with a glowing border while the incognito session is in progress.

## Consequences

- **Positive:**
  - A much faster, smoother and more discreet user experience: quick pasting without obstructing the work screen.
  - Elimination of redundant hotkeys and simplification of the configuration model.
  - Fast, intuitive search immediately accessible by typing directly.
  - Visual and identity consistency across all app states.
- **Negative / Challenges:**
  - The detailed horizontal cards view (`PopupWindow`) remains accessible primarily through the system tray context menu.
