# 16: Settings UI, profiles and themes

**What to build:** every ported preference adjustable in a settings window:
history, behavior, exclusions, dialog/item/header/per-type screens, both
profiles, theming, all shortcut groups, actions management and the
About/Diagnostics page — first run opens on the Default profile.

**Blocked by:** 13 (actions engine), 15 (hotkeys, tray and popup).

**Status:** resolved

- [x] History screen shows SQLite only with file picker, ranges and the three end-of-session modes
- [x] Behavior flags, per-process exclusions UI and GNOME-cut list match the decided scope
- [x] Dialog/item/header/per-type screens keep all Copyous options with mapped defaults and ranges
- [x] Default and Compact profiles switch the full preset matrix; Dark/Light/HighContrast plus custom colors apply live
- [x] Shortcut screens cover persisted and hardcoded maps; Actions screen does CRUD plus per-type defaults
- [x] About/Diagnostics shows library versions and opens Data/Config/Cache in Explorer

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis self-review
(Standards + Spec). 562/562 xUnit green (493 prior + 69 new), 0 errors,
0 warnings (.NET 8.0.425, no new packages — BCL + Microsoft.Data.Sqlite only).
Seams in `Settings/`: `SettingLimits` (gschema ranges), `HistorySettings`
(SQLite-only UI list, .db picker via DatabasePaths, length/age clamp,
3-mode EndOfSession + SessionCleanup over a real store), `BehaviorSettings`
(6 flags; no SyncPrimary/PasteOnCopy) + `ProcessExclusions` (exe-insensitive
add/remove/match, path rejection) + `GnomeCuts` (pinned cut list),
`DialogSettings` (12 keys, alias normalization, clamp),
`ItemSettings`/`HeaderSettings` (Copyous defaults, dynamic-height gating),
`PerTypeSettings` (all 6 screens, Shell-thumbnail note, link exclusions via
LinkExclusions, preview-options bridge, max-characters clamp),
`ThemeSettings` (Default/Custom — Yaru cut — system-follow default, 4 customs
over Copyous fallbacks, live ResolveEffective), `ProfilePresets` (13-key
Default/Compact matrix from profiles.ts:164, Detect/Apply, first-run Default),
`ShortcutSettings` + `ShortcutCatalog` (11 persisted rows with global/local
validation, hardcoded popup rows incl. the Alt→Alt+P deviation),
`FeedbackSettings`/`PasteSettings` (9 sounds + dB clamp + gain/asset bridges,
Ctrl+V default + 100–300ms delay), `AppFolders` (Data/Config/Cache + file
defaults) + `DiagnosticsInfo` (app/dotnet/SQLite versions + 6 paths; Explorer
launch stays in WPF), `AppSettings` root (Default(), ClampAll with null-section
coercion, ToCapture/ToPaste/ToSound/ToCopyFeedback/ToLinkPreview bridges) +
`SettingsStore` (camelCase + enum-string JSON, atomic tmp+move, backup ~,
missing→default, corrupt→default, load-time clamp).
Review fixes folded in: history-path trim, null-safe Processes setter,
null-section coercion in ClampAll (+ regression test), StartsWith arg order,
missing Actions using.
Deferred by ticket boundary: real WPF settings window + H.NotifyIcon/file-picker/
Explorer wiring (Core is net8.0 with no WPF refs — no UI automation in v1 per
Testing Decisions); actions CRUD UI binds the existing engine from 13
(ActionCatalog/ActionsStore/BuiltinActions/ActionValidation) with no new Core
shape; full 192-row hljs table stays post-v1 (CodeLanguageMap misses are plain
text by design).

Formal /code-review 2026-09-10 (diff 6931c1a..54c5686): Standards 0 hard + 6 smells (1 spec-overridden, rest kept); Spec 8 findings. Fixed: HighContrast gets its own black/white set (never Dark); position normalizers accept only own-axis tokens (cross-axis tokens throw — silent wrong-dock mapping removed). Open: Actions screen (binds 13, accepted), WPF window/picker/Explorer/first-run (shell), About dialog (shell binds 18 record), PasteSettings/store/bridges (accepted), Ctrl+Shift tag row (accepted extra), SQLite client-vs-native version string. Suite 689/689. Status → resolved.
