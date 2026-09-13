# 01: Retire tags from UI context menu and keyboard shortcuts

**What to build:** Remove the "Tags" submenu from the popup right-click context menu and disable tag-cycling and tag-assignment keyboard shortcuts (`Ctrl+Shift+1..9`, `Ctrl+\``), ensuring that card colors and badges are no longer influenced by obsolete tag slots while keeping database compatibility intact.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] "Tags" submenu is removed from `PopupWindow.xaml.cs` item context menu
- [x] Tag cycling and tag slot assignment actions are removed or bypassed in `PopupKeyboardMap.cs`
- [x] Existing card stripe converter falls back directly to kind/category accent color
- [x] Unit tests verify keyboard mappings no longer produce tag actions and clear history protects pinned items without tag dependency
