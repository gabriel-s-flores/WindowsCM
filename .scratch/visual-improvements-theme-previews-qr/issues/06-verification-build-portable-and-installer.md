# 06: Automated test verification and release compilation (portable and installer)

**What to build:**
Run the complete automated test suite (`dotnet test`), compile the single-file Release binary for `win-x64`, build the portable distribution (`dist/WindowsCM-portable.zip`), and compile the Inno Setup installer (`WindowsCM-Setup-1.0.0.exe`).

**Blocked by:** 01, 02, 03, 04, 05

**Status:** resolved

- [x] All automated tests in `dotnet test` pass with 0 failures and 0 warnings
- [x] `dotnet publish` generates `installer/publish/WindowsCM.exe`
- [x] Build and zip portable distribution in `dist/WindowsCM-portable.zip`
- [x] Compile Inno Setup installer with `ISCC.exe` to `dist/WindowsCM-Setup-1.0.0.exe`
