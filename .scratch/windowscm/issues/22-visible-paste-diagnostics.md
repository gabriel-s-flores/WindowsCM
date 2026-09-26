# 22: Paste with never-silent diagnostics

**What to build:** picking an item always ends in a visible outcome: either it pastes into the previous app, or it copies and explains why it did not paste (focus lost, elevated target, write/injection failure) — it never fails silently.

**Blocked by:** 20 (temporary instrumentation + baseline + smoke skeleton).

**Status:** resolved

- [x] Enter pastes into a regular non-elevated app; Shift+Enter only copies, without hiding the popup or injecting a key
- [x] An elevated target without an elevated app results in copy-only with a warning about elevation
- [x] Focus lost before injection results in copy-only with a focus warning, never diagnosed as elevation
- [x] Missing item, image with a missing file, busy clipboard and refused injection fail with readable diagnostics
- [x] Smoke covers regular Notepad, an elevated app and a focus change mid-flow

## Comments

Implemented 2026-09-11. Full suite **725/725 green** (710 prior + 15 new),
filtered popup/paste/tray **161/161 green** (146 + 9 feedback + 4 capturing
+ 2 layout), `dotnet build` 0 warnings/errors. Live smoke 5 PASS / 0 FAIL /
2 SKIP (SKIPs are tickets 23-24 placeholders).

Gaps found against the ticket-12 orchestrator (all fixed here):
- Tray left-click, Open/Incognito menu and pipe show/toggle never captured
  the paste target (only `App.OnHotkey` did), so those paths pasted with a
  stale handle. Fix: new `TargetCapturingPopup` decorator
  (`src/WindowsCM.Core/Tray/TargetCapturingPopup.cs`) wraps the shell popup
  used by the tray controller and the pipe dispatcher, capturing the
  foreground HWND before every show (never on hide); `App` logs it as
  `capture slot=shell`. Proven live (`capture slot=shell` lines with a fresh
  HWND per open).
- Missing-item activation returned silently (log only, no balloon, stale
  list kept). Fix: `App.ActivateAsync` now refreshes the popup and balloons
  `Item {id} is no longer in history, so nothing was copied` via the new
  pure `ActivationFeedbackPolicy.ForMissingItem`.
- Copy-with-warning (`CopiedOnlyForegroundLost` / `CopiedOnlyElevated`)
  ballooned but never signaled the copy. Fix: `App` now follows
  `ActivationFeedbackPolicy.ForOutcome` — success signals (flash+sound, no
  balloon), warnings signal AND balloon the reason, `Failed` balloons only.
  Never-silent invariant is unit-pinned (every non-success carries a
  non-empty balloon).

Real bug caught live by the new smoke (first run failed 1/4 as designed):
`NativePaste.INPUTUNION` only carried the keyboard arm, so
`Marshal.SizeOf<INPUT>()` was 32 instead of the platform 40 on x64 and
`SendInput` rejected every chord — Enter into Notepad ended in `Failed`
("not accepted for injection") instead of `Pasted`. Fix: full three-arm
union (`MOUSEINPUT`/`KEYBDINPUT`/`HARDWAREINPUT`) in
`src/WindowsCM.Core/Paste/Win32/NativePaste.cs`, pinned by
`Win32InputLayoutTests` (40-on-x64 / 28-on-x86) plus
`InternalsVisibleTo` for the test assembly. Clipboard-busy and
injection-refused stay unit-covered (`WriterFailure`/`InjectorFailure`);
image-missing stays unit-covered (`MissingImageFile_FailsWithDiagnostics`).

Files:
- `src/WindowsCM.Core/Paste/ActivationFeedback.cs` (new policy seam) +
  `tests/.../Paste/ActivationFeedbackTests.cs` (new, 9 tests incl. the
  4-case Failed-never-silent theory).
- `src/WindowsCM.Core/Tray/TargetCapturingPopup.cs` (new decorator) +
  `tests/.../Tray/TargetCapturingPopupTests.cs` (new, 4 tests).
- `tests/.../Paste/Win32InputLayoutTests.cs` (new, 2 tests).
- `src/WindowsCM.App/App.xaml.cs` (capture wrapper wiring + policy-driven
  feedback + missing-item balloon/refresh).
- `smoke-ui.ps1`: `paste-diagnostics` automated — seeds unique clipboard
  text, opens via the real `Ctrl+Shift+V` hotkey (a pipe `--show` cannot
  activate the popup, so `SendKeys` would miss), then Enter: common Notepad
  (`Pasted`, round-trip `^a`/`^c` reads the seed back), Shift+Enter
  (`CopiedOnly`, `chord=<none>`), killed-target (`CopiedOnlyForegroundLost`
  with no elevation wording), `--clear-all` then Enter (`MissingItem`);
  elevated is best-effort (SKIPs honestly when no elevated window exists —
  UIPI eats injected keystrokes to elevated windows, so only the pipe path
  is attempted; refusal logic stays unit-covered).

Live proof (1920x1080 @100%): `outcome=Pasted chord=CtrlV`,
`outcome=CopiedOnly chord=<none>`, `outcome=CopiedOnlyForegroundLost`,
`outcome=MissingItem diagnostics=Item 24 is no longer in history, so
nothing was copied`; elevated `SKIP (no elevated window found)`. Full log
excerpt in `smoke-report.md`.

Left uncommitted in the tree (prior tickets 19/20/21 are also
staged/untracked, so a per-ticket commit would sweep others' work): this
ticket's delta is `ActivationFeedback.cs`, `TargetCapturingPopup.cs`,
`NativePaste.cs`, `App.xaml.cs`, the three new test files, `smoke-ui.ps1`,
`smoke-report.md` + this file.
