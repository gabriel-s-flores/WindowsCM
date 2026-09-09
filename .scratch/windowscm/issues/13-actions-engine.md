# 13: Actions engine

**What to build:** default and custom actions run against items: the JSON
model with CRUD and reset, type-plus-regex matching, shell execution with
timeouts, the ported default set, per-type defaults, QR codes and color
conversions.

**Blocked by:** 10 (classifier and content types).

**Status:** resolved

- [x] actions.json loads, saves, merges and resets to built-in without losing custom entries
- [x] Matching combines type subset with regex (documented engine semantics, match timeout, bad pattern = no match)
- [x] Commands run with content on stdin, stdout captured trimmed, 30s timeout, placeholder substitution
- [x] Ported defaults work: shell open, Explorer reveal, browser open, paste-as-path, color conversions, QR on its shortcut
- [x] Per-type default mapping resolves (including submenus) for the popup chord

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis code-review.
338/338 xUnit green (237 prior + 101 new), 0 errors, 0 warnings (.NET 8.0.425,
no new packages — BCL only).
Seams: `ActionCatalog` (flatten/find-by-id incl. submenus, is/find-default),
`ActionsJson` (case-insensitive, comments, trailing commas, unknown-kind
keeper, unknown props ignored), `ActionsStore` (atomic tmp+move, tab indent,
`~` backup, sentinel/env/`%AppData%` paths), `BuiltinActions` (Windows
defaultConfig + merge-missing/count-difference), `ActionMatcher` (2s timeout,
bad-pattern/timeout = no-match, Unicode semantics documented),
`CommandLine` (%N canonical + $N alias, trailing argv), `PathRewriter`
(file:// strip + unescape, spec-literal order), `ColorConverter` (full
color.ts toColor/toString port), `QrActions` (five text-like kinds, Ctrl+Q),
`ActionExecutor` over `IProcessRunner`/`IShellLauncher` fakes
(stdin/stdout-trim/30s-kill, native routing for the four ported ids,
copy/paste/QR dispositions) + `ActionValidation` (CRUD guards, Guid ids).
Real `ProcessRunner` (async drain, whole-tree kill) and `ShellLauncher`
(UseShellExecute opens, explorer /select reveal) compile unwarned; live
behavior is manual-smoke per Testing Decisions. QR bitmap rendering stays
in the UI layer (QRCoder); Core hands over eligibility + payload.
Review fixes folded in: invariant numerals in Format (comma-locale leak),
case-insensitive DOM reads, Test guard on native branches, env-first path
precedence (DatabasePaths parity), UnknownAction hidden from Applicable,
entry→action wording (CONTEXT.md).
Deferred by ticket boundary: FileSystemWatcher live reload + gesture
parsing (UI layer, 15/16); SHOpenFolderAndSelectItems PIDL reveal (v1.1);
QRCoder bitmap dialog (15); actions CRUD UI (16).
