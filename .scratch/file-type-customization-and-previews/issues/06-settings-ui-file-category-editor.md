# 06: Settings UI for File Category Customization

**What to build:** Expand the "Cores dos Tipos" (Type Colors) tab in `SettingsWindow` to include a full file category and extensions editor: list of categories with name, accent color badge and hex box, assigned extensions, buttons to add a new category, remove custom category, edit extensions, and restore Windows defaults with instant live update in popup cards.

**Blocked by:** 05

**Status:** resolved

- [x] File categories editor UI in `SettingsWindow.xaml`
- [x] Binding and live updating logic in `SettingsWindow.xaml.cs`
- [x] Add new custom category dialog / inline controls
- [x] Edit extensions and color hex for existing categories
- [x] "Restaurar Padrões do Windows" button to reinitialize default associations
- [x] Changes immediately notify popup and save to `AppSettings`
