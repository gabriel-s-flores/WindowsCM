# 03: Windows File Type Resolver with Registry Fallback

**What to build:** Introduce `WindowsFileTypeResolver` to classify file extensions against user categories first, and for any unmapped extension, inspect Windows Registry `HKEY_CLASSES_ROOT\<ext>\PerceivedType` and shell associations dynamically, returning appropriate friendly category and default color.

**Blocked by:** 02: File Category Settings Model

**Status:** resolved

- [x] Interface and service for querying Windows file associations and `PerceivedType`
- [x] Safe fallback when registry entries do not exist or when running in test environments
- [x] Resolution pipeline: User Category -> Windows PerceivedType -> Generic File
- [x] Unit tests covering known extensions, Windows fallback simulation, and edge cases
