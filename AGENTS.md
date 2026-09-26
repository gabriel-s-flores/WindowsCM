# WindowsCM — Copyous for Windows

A clone of Copyous (GNOME) for Windows. See the `WindowsCM` Obsidian vault at `C:\Users\gabri\Documents\obsidian\WindowsCM` (readable mirror) and `CONTEXT.md` / `docs/adr/` (canonical in the repo once created).

## Agent skills

### Issue tracker

Local markdown in `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context (`CONTEXT.md` + `docs/adr/` at the root). See `docs/agents/domain.md`.

## Delivery and Build Rules

### English Only, in the Repository and on GitHub
- **Everything is written in English**: code, comments, identifiers, docs (`CONTEXT.md`, `docs/`, `.scratch/`), file names, scripts and their output, commit messages, branch names, pull request titles and descriptions, review comments and release notes. Keep the existing style: Conventional Commits subjects (`fix(popup): ...`) and plain, factual bodies.
- **Never mix Portuguese and English.** When referring to UI text, quote its English string.
- **The only Portuguese allowed is product data**: the pt-BR UI strings (`PortugueseAppStrings` and the other localized branches) and the test fixtures that assert or feed them.

### Strict Localization Parity (Portuguese and English)
- **Zero Hardcoded Strings**: Hardcoding user-visible text (titles, buttons, hints/tooltips, descriptions, menu items, alerts, layout badges, time formats, etc.) in C# code or XAML files is expressly forbidden.
- **Mandatory Parity**: Every new feature or UI change MUST implement absolute (100%) parity between Portuguese and English in `IAppStrings`, `PortugueseAppStrings` and `EnglishAppStrings`.
- **XAML Dynamic Resources**: In XAML, text must use `{DynamicResource Loc_<Property>}`. When the language is switched, the resource dictionary must be replaced at runtime so that every element of the open windows is invalidated dynamically.
- **Automated Regression Tests**: New strings added to `IAppStrings` must be covered by the tests in `LocalizationTests.cs`, ensuring that no property returns null or empty in either language.

### Mandatory Build (Portable and Installer) After Every Task
After finishing any task or change in the project, you must build and provide both the portable version and the installer executable, **always in the same folder** (`dist/`):
- **Portable**: `dist/WindowsCM-portable/` (single-file executable `WindowsCM.exe` + `LICENSE`) and the zip file `dist/WindowsCM-portable.zip`.
- **Installer**: `dist/WindowsCM-Setup-1.0.0.exe` (generated with Inno Setup).

To build both targets into `dist/` automatically in a single step, run:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dist.ps1
```
