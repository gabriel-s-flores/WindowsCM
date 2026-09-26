# 27: Full-Width Popup, Smooth Scrolling, PT-BR Localization, Per-Card Menu and Emojis

Type: task

Status: resolved

Blocked by: 26

## What to build:
1. **Full screen width (Horizontal Fill)**: The horizontal popup must take up the usable width of the screen (with comfortable 16-24px side margins, Copyous style `clipboard-position-horizontal: fill`), making it possible to view multiple cards (6-8) side by side on screen instead of a narrow fixed 880px box.
2. **Smooth horizontal scrolling (Smooth Scroll)**: When scrolling with the mouse wheel or the navigation keys, card scrolling must be smoothly animated (with ease-out interpolation over ~200ms and per-pixel units), eliminating the abrupt jumps that bothered users.
3. **100% Portuguese (PT-BR) localization**: Eliminate the mix of English and Portuguese in the interface. All user-facing text (tray menus, Settings window, buttons, dialogs, tooltips and messages) must be unified in Portuguese.
4. **Dedicated menu and options at the top of each card**: Copyous parity in each card's header:
   - Pin/unpin button right on the card (`Pin`).
   - Card menu button (`...`), opening the context menu with the item's options (Copy, Paste, Pin, Actions, Tags, Edit title/content, Delete).
   - Quick-delete button right on the card (`Delete`).
   - Friendly relative time in the header ("just now", "5m ago", "2h ago").
   - The menu also opens by right-clicking the card.
5. **Rich display of Emojis and Characters**:
   - For `Character` / Emoji items, show a dedicated preview in the `Segoe UI Emoji` font at a large size (52-56px), centered vertically and horizontally on the card, identifying the Unicode code point (e.g. `U+1F680`) and with a friendly subtitle ("Emoji • U+1F680").
   - `Segoe UI Emoji` support in the general text preview so that emojis in normal text also render in color and crisply.
6. **Automated test suite**: Update and expand the unit tests in `WindowsCM.Core.Tests` to validate emoji formatting, full-screen sizing calculations and the absence of regressions.

## Answer
Implemented on 2026-09-12. All 5 requirements were completed and validated:
1. **Full width (Horizontal Fill)**:
   - `CalculateHorizontalFillWidth` and `PlaceHorizontalFill` methods created in `PopupPlacement.cs`.
   - `PopupWindow.xaml` and `PopupWindow.xaml.cs` now expand to fill the usable width of the active monitor's work area with 20px margins, making it possible to view 6 to 8 cards at once.
2. **Smooth horizontal scrolling (Smooth Scroll)**:
   - Implemented support for continuous per-pixel animation via the `AnimatedOffsetProperty` attached property with `DoubleAnimation` and a `QuadraticEase` curve (EaseOut, 200ms).
   - Accumulation of mouse-wheel notches (`Math.Clamp`), eliminating rigid jumps and stutters.
3. **100% PT-BR localization**:
   - Tray menus (`TrayMenu.cs`: Open, Incognito mode, Clear history, Settings, Exit).
   - Settings window (`SettingsWindow.xaml` and `.cs`: Keyboard Shortcuts, Startup, System Tray, Folders, Versions, Credits, guidance and shortcut feedback).
   - Dialogs and messages (`TextInputDialog`: Cancel / OK; `QrWindow`: QR Code; `App.xaml.cs`: balloons and notices in Portuguese).
   - Type and counter texts (`ItemDisplayFormatter.cs`: "Text • {len} characters", "File", etc.).
4. **Dedicated per-card menu and buttons (Copyous parity)**:
   - Each card's header in `PopupWindow.xaml` updated with:
     - Localized relative time (`RelativeTimeConverter`: "just now", "5 min ago", "2h ago").
     - Pin/unpin button (`OnCardPinButtonClicked`).
     - `...` menu button (`OnCardMenuButtonClicked`).
     - Quick-delete button (`OnCardDeleteButtonClicked`).
   - Right-clicking the card (`OnCardMouseRightButtonUp`) and the `...` button open `ShowCardContextMenu`: Paste, Copy, Pin/Unpin, applicable Actions, Generate QR code, Tags submenu (9 colors + remove tag), Edit title, Edit content and Delete.
5. **Rich Emoji display**:
   - `ItemDisplayFormatter`: emoji detection via `IsEmoji`, Unicode code point formatting `GetUnicodeCodePoint` (e.g. `U+1F680`), "Emoji" titles and "Emoji • U+1F680" subtitles.
   - `PopupWindow.xaml`: dedicated container for `CharacterPreviewVisibility` with `FontFamily="Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji, Segoe UI Symbol"`, centered at 54px, with a monospaced Unicode code.
   - `Segoe UI Emoji` support also added to the general text and code preview.
6. **Automated tests**:
   - 9 new tests added in `ItemDisplayFormatterTests` and `PopupPlacementTests`.
   - Suite of 755 tests run with 100% success and 0 warnings/errors.

