# 02: File Category Settings Model

**What to build:** Introduce a rich `FileCategorySettings` configuration model in `WindowsCM.Core.Settings` with default categories (Imagens, Vídeos, Áudio, Documentos, Planilhas, Apresentações, Código, Compactados), extension matching (case-insensitive), color customization, addition/removal of custom user categories, and persistence within `AppSettings`.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] `FileCategory` record/class representing a category with `Id`, `Name`, `ColorHex`, `Extensions`, and `IsBuiltIn`
- [x] `FileCategorySettings` with standard defaults matching Windows file types
- [x] Helpers: `ResolveCategory(string extension)`, `AddCategory(...)`, `RemoveCategory(...)`, `ResetToDefaults()`
- [x] Integrated into `AppSettings` and tested for round-trip JSON serialization
- [x] Comprehensive unit tests in `FileCategorySettingsTests.cs`
