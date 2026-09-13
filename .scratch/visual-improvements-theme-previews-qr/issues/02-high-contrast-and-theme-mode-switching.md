# 02: High contrast theme palette and scheme switching support

**What to build:**
Implement the High Contrast theme palette in `PopupThemeBrushes.cs` (pure black `#000000` background, high contrast `#FFFFFF` borders, crisp readable text, `#00FFFF` selection, `#FFFF00` pinned items) and update `PopupThemeBrushes.CreateThemeDictionary` to accept `ColorScheme` directly.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Add High Contrast color tokens and frozen brushes in `PopupThemeBrushes.cs`
- [x] Support `CreateThemeDictionary(ColorScheme scheme, ItemColorSettings? customColors = null)`
- [x] Update `App.xaml.cs`, `PopupWindow.xaml.cs`, and `CompactPopupWindow.xaml.cs` to apply theme by `ColorScheme`
- [x] Unit tests asserting High Contrast color resolution and contrast compliance

