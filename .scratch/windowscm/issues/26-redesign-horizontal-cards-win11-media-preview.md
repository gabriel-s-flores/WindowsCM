# 26: Redesign with Horizontal Cards, Windows 11 Identity and Expanded Previews

**Type:** task

**Status:** resolved

**Blocked by:** 21, 23

**What to build:**
1. Clean formatting of file and media items: extract only the file name (`Path.GetFileName`) and a descriptive type ("PNG Image", "MP4 Video", "C# Code"), never exposing the absolute path or `file:///...`.
2. Enable thumbnail loading for image files copied from Explorer (`ItemKind.File`).
3. Horizontal card layout (250x230px) in the popup (880x320px), in a horizontal scroll container with support for the mouse wheel and the keyboard arrow keys.
4. Enlarged preview for code and text in a monospaced font (`Cascadia Code` / `Consolas`), allowing 6 to 8 lines to be viewed.
5. Top bar action buttons with native `Segoe Fluent Icons` / `Segoe MDL2 Assets` icons (32x32px), accented states for Pinned and Incognito, and informative ToolTips with shortcuts.
6. Update the sizing and placement unit tests and create new tests for `ItemDisplayFormatter`.

- [x] File/media formatting without absolute paths and with thumbnails enabled
- [x] Expanded code and text previews in horizontal cards
- [x] Bar buttons with Segoe Fluent Icons glyphs and no clipped text
- [x] Windows 11 Fluent Design visual identity
- [x] Unit test suite 100% green

## Answer
Implemented on 2026-09-12. All requirements met:
1. `ItemDisplayFormatter` created in `WindowsCM.Core.Popup` with pure, 100% tested logic (10 new tests): file name extraction, type/extension identification (PNG, JPEG, GIF, MP4, MP3, PDF, C#, etc.), multi-line preview and Segoe Fluent Icons glyphs.
2. `PopupConverters.cs` updated:
   - `TitleLineConverter` delegates to `ItemDisplayFormatter.GetTitle` (never exposes absolute paths or `file:///...`).
   - `KindLabelConverter` shows human-readable types.
   - `ImageThumbConverter` now loads both cached images and local image files from Windows Explorer at a higher resolution (`DecodePixelHeight = 180`).
   - Visibility converters and dedicated previews added.
3. `PopupWindow.xaml` redesigned to 880x320px with horizontal cards (250x240px), fluid horizontal scrolling (mouse wheel and keyboard arrow key support), a Windows 11 Fluent search bar with a magnifier icon, and compact 32x32px buttons with Segoe Fluent Icons (`\uE718` Pin, `\uE727` Incognito, `\uE74D` Clear, `\uE713` Settings) and accented states.
4. Unit tests updated in `PopupSizingTests` (880x320) and `PopupPlacementTests` (horizontal 1080p clamping). Full suite with 746 green tests and zero warnings/errors.
5. ADR 0001 recorded in `docs/adr/0001-horizontal-cards-layout-windows11.md`.
