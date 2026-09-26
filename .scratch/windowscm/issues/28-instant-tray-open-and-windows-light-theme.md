# 28: Instant Opening from the Tray and Windows Light Theme Support

Type: task

Status: resolved

Blocked by: 27

## What to build:
1. **Instant opening and response when clicking the tray icon (System Tray)**:
   - Eliminate the 500ms delay timer (`_clickTimer` / `DoubleClickTime`) in `TrayManager`.
   - Fire the popup toggle/open immediately, in 0ms, on detecting a left-button click on the icon.
   - Protection against Explorer's focus-loss races / *deactivation bounce* (especially when opening from the Windows 11 tray overflow).
   - Coordination between WPF's `Deactivated` event and the tray click so that the closing click does not reopen the popup.
   - Optimization of the `PopupWindow` opening pipeline (eliminate duplicate SQLite queries in `ShowAtCursor` and guarantee foreground activation via `SetForegroundWindow`).

2. **Native support for the Windows Light Theme (Dynamic Light & Dark Themes)**:
   - Detection of the Windows operating system theme (via the `Personalize\AppsUseLightTheme` registry value and user-preference change events).
   - Integration with Core's `ThemeSettings` and `ColorScheme.System`.
   - Complete set of Windows 11 Fluent brushes and colors for Light Mode and Dark Mode in `PopupWindow`:
     - Popup background and borders.
     - Search field, text and magnifier icon.
     - Top action bar buttons and card buttons.
     - History cards (white background in light mode with subtle borders and Fluent blue hover/selected states).
     - Adapted preview areas (GitHub/VS Light style code, high-contrast text, characters/emojis and thumbnails).
   - Dynamic real-time update when the user changes the theme in Windows Settings or in the app's preferences.

3. **Automated tests and zero regressions**:
   - Unit tests for the Windows theme detector and color scheme resolution.
   - Validation of the full existing test suite (762 tests passing).

## Answer
Implemented and validated on 2026-09-12:

1. **Instant tray opening and response (0ms)**:
   - `TrayManager.cs`: complete removal of `_clickTimer` (500ms `DoubleClickTime` interval). The left-button `MouseClick` event directly invokes `_controller.OnLeftClick()` on the application `Dispatcher` without any artificial delay.
   - `TrayManager.cs`: `DoubleClick` mapped to `_controller.OnDoubleClick()`, ensuring that a user who double-clicks the tray icon sees the app open and stay in the foreground (without accidentally toggling/closing).
   - `PopupWindow.xaml.cs`: added a focus-transition safeguard in `OnDeactivated`. If focus loss is reported within the first 250ms after opening, the event is ignored (preventing Explorer's capture release in the tray overflow from closing the just-opened popup).
   - `PopupWindow.xaml.cs` and `ShellAdapters.cs`: implemented close coordination via `WasRecentlyHidden` (350ms) in `Toggle()`. When the popup is open and the user clicks the tray icon to close it, the click that caused the focus loss and the close does not reopen the popup by mistake.
   - Direct call to User32's `SetForegroundWindow` on open, guaranteeing immediate foreground.
   - Removal of a redundant SQLite query on open when the search is already empty.

2. **Dynamic Light Theme integrated with Windows**:
   - `IWindowsThemeDetector.cs` in `WindowsCM.Core/Settings/`: contract for system theme detection and change notification via the `ThemeChanged` event.
   - `ThemeDetector.cs` (`Win32WindowsThemeDetector`) in `WindowsCM.App`: reader of the Windows Registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` with a high-contrast check and a subscription to `SystemEvents.UserPreferenceChanged` to react immediately when Windows switches between Light and Dark modes.
   - `PopupThemeBrushes.cs` in `WindowsCM.App`: resource dictionary generator with a complete Windows 11 Fluent palette in Light Mode and Dark Mode:
     - Window background (`#F3F3F3` Light, `#202020` Dark).
     - Cards (`#FFFFFF` Light with `#E2E2E2` border and `#F8F8F8` hover; `#2B2B2B` Dark).
     - High-contrast primary and secondary text (`#1C1C1C`/`#5F5F64` Light; `#FFFFFF`/`#8E8E93` Dark).
     - Search box (`#FFFFFF` Light with `#CECECE` border and dark text).
     - Header and card buttons with hover and pressed colors adapted to each mode.
     - Code previews in a light editor style (`#F6F8FA` with dark text `#24292E` and `#E1E4E8` border).
     - Image, character/emoji and text previews styled according to the active mode.
   - `PopupWindow.xaml`: all hardcoded colors replaced with `{DynamicResource}` references.
   - `PopupWindow.xaml.cs`: the `ApplyTheme(bool isLight)` method updates the dynamic resources instantly without destroying or rebuilding the visual tree.
   - `App.xaml.cs`: theme detector initialization and runtime synchronization via the event and when the Settings window closes.

3. **Validation and tests**:
   - `WindowsThemeDetectorTests.cs`: 7 new unit tests covering simulation of a light system, a dark system, high contrast, the dynamic change event and settings precedence.
   - 762 automated tests run with 100% success and zero regressions.
   - Debug and Release builds with 0 errors and 0 warnings.
