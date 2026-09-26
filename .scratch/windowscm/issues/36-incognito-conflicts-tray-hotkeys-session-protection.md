# Issue 36: Resolving Incognito Mode Conflicts (Tray, Hotkeys and Session Protection)

Status: resolved
Type: fix
Blocked by: 35

## Context

The user identified conflicting behaviors and accidental data loss in incognito mode:
1. When clicking the tray (system tray) after turning on incognito mode, the mode was silently turned off and the entire ephemeral history was lost.
2. When using the incognito mode hotkey (`Ctrl+Shift+Alt+V`) with the popup open in order to close it (toggle), the app called `SetIncognito(false)`, destroying the clips.
3. When using the normal hotkey (`Ctrl+Shift+V`), the popup did not close as a toggle, and there was no way to switch between the normal and incognito history without losing the session.

## Requirements

1. **Visibility Non-Destructiveness Invariant**:
   - `Show`, `Hide`, `Toggle`, tray icon clicks and window-closing shortcuts must NEVER turn off incognito mode or clear data.
   - Only explicit actions (`[Exit incognito]`, an intentional toggle in the menu/header, or shutdown) may destroy the session.
2. **Tray and Adapters Fix**:
   - `ShellPopup.Toggle()` and `TrayController.OnMenu(TrayMenuItem.Open)` must respect the active mode (`IsIncognito`) instead of forcing `incognito: false`.
3. **Pure PopupViewModel**:
   - `PopupViewModel.Show` must not suffer the destructive side effect of calling `SetIncognito`.
   - Support for switching the view between the normal history and the incognito session without ending the ephemeral session.
4. **Predictable Hotkeys**:
   - `Ctrl+Shift+V` and `Ctrl+Shift+Alt+V` must close the popup if it is already visible (clean Toggle).
5. **TDD Cycle**:
   - Unit tests for `TrayController`, `PopupViewModel`, `IncognitoSessionCoordinator` and `App`.
