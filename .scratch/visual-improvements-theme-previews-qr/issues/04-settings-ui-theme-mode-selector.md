# 04: Settings UI theme mode selector (Dark, Light, High Contrast, System)

**What to build:**
Add an interactive theme selection card in `SettingsWindow.xaml` allowing the user to select between Dark Mode, Light Mode, High Contrast Mode, and Follow System. Persist the selection to `settings.json` and immediately apply the theme to all open windows.

**Blocked by:** 02

**Status:** resolved

- [x] Add theme selection card and controls in `SettingsWindow.xaml`
- [x] Implement event handler in `SettingsWindow.xaml.cs` to update `_settings.Theme.Scheme`
- [x] Save settings and notify active app windows (`PopupWindow`, `CompactPopupWindow`, `SettingsWindow`)
- [x] Update theme status text in the navigation pane
