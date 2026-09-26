# 32: Settings Visual Refactoring, 100-Item Limit, Cache Clearing and Custom Colors per Type

Type: task

Status: resolved

Blocked by: 31

## User Report / Requirements:
1. Visual refactoring of the Settings area to the modern Windows 11 Fluent Design standard.
2. A setting for the clipboard's maximum limit: 100 as both the maximum and the recommended value.
3. A setting to clear the cache directly in the app, with real-time feedback.
4. A setting to customize the colors of the file, link, code and other clipboard item types.
5. Execution via Matt Pocock's flow (agreed seams, deep modules, TDD in red-green vertical slices).

## Answer
Implemented and validated on 2026-09-12, rigorously following Matt Pocock's flow (`codebase-design`, `domain-modeling` and `tdd`):

1. **Maximum and Recommended Clipboard Limit (100 items)**:
   - Updated `SettingLimits.cs` with `HistoryLengthMax = 100` and `HistoryLengthDefault = 100` (range 10..100).
   - `HistorySettings.cs` and `AppSettings.cs` automatically clamp to the new limit range.
   - Added a Fluent slider in the Settings window with a live display (`100 items`) and a "Restore recommended (100)" button.
   - Real-time updates trigger `_store.Evict` to truncate excess items immediately when the limit is lowered.

2. **Cache Clearing Directly in the App**:
   - Created the deep module `CacheCleaner.cs` in `WindowsCM.Core.Settings`: pure static method `Clear(string cacheDir) -> CacheCleanResult`.
   - Safely deletes all cache files and subdirectories (favicons, link thumbnails, temporary files), preserving the integrity of the history and of the data/settings directories.
   - Returns the count of deleted files and freed bytes, tolerating files locked by I/O.
   - Interface with a "Clear Cache Now" quick-action button and immediate real-time visual feedback in the Settings window.

3. **Item Type Color Customization**:
   - Created the `ItemColorSettings.cs` model in `WindowsCM.Core.Settings`: properties for each `ItemKind` (`Link`, `Code`, `File`, `Image`, `Character`, `Color`, `Text`), Hex normalization methods (`#RGB`, `#RRGGBB`), validation and reset.
   - Integrated into `AppSettings.cs` with clamping and persistence in `settings.json`.
   - Updated `ItemTypeTheme.cs` to support `ItemColorSettings?`, automatically computing the translucent background (alpha 12% in the Light theme, 15% in the Dark theme) from the custom color.
   - Updated `PopupThemeBrushes.cs` to generate dynamic brushes from `ItemColorSettings`.
   - In the Settings window, a complete list with a live preview of a sample badge for each type, Hex input boxes, "Choose color" buttons via the native picker, an individual "Default" button and "Restore all default colors".
   - Immediate synchronization: when a color is changed, the application and popup themes are updated instantly in real time.

4. **Complete Visual Refactoring of the Settings Window (Windows 11 Fluent Design)**:
   - Redesigned the `SettingsWindow.xaml` and `SettingsWindow.xaml.cs` interface with a modern two-column layout (Sidebar with Fluent icons and tab navigation).
   - Dynamic support for the Windows 11 Light and Dark themes with brushes from `PopupThemeBrushes`.
   - Rounded cards with a typographic hierarchy (`Segoe UI Variable Text`), subtle borders and shadows.
   - 5 well-structured sections: History & General, Item Colors, Cache & Storage, Global Shortcuts and About & System.

5. **Unit Tests & Quality**:
   - Strict Test-Driven Development (TDD) in vertical slices with Red → Green cycles.
   - Created `CacheCleanerTests.cs` and `ItemColorSettingsTests.cs`.
   - Updated `HistorySettingsTests.cs`, `AppSettingsTests.cs` and `ItemTypeThemeTests.cs`.
   - A total of 839 tests run with 100% success and 0 warnings/errors.
