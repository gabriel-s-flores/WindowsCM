# WindowsCM release smoke matrix (ticket 18)

Release gate: no version ships unless the automated gate is green AND
every row below is signed off on the listed environments. Automation
covers what it can (`dotnet test`, `InstallerScriptTests` pinning the
`.iss` contract); this matrix covers what it cannot (Testing Decisions
in the spec): real Win32 capture, elevated targets, hotkey conflicts,
tray gestures, multi-monitor/DPI placement, and the
installer upgrade/uninstall matrix.

Environments: Windows 10 20H2 (build 19042, floor) and Windows 11;
DPI 100/150/200%; single + multi-monitor (different DPI per monitor).

## 0. Automated gate

- [ ] `dotnet test WindowsCM.sln`: all green (671 at ticket 18), 0 warnings, 0 errors
- [ ] Every `.cs` under `src/`, `tests/`, `prototype/` starts with
      `// SPDX-License-Identifier: GPL-3.0-or-later`
- [ ] `LICENSE` at root is the verbatim GPL-3.0 text; About shows
      `GPL-3.0-or-later` with Copyous/Pano + bundled-library credits

## 1. Build and package

Release layout: single-file publish staged into `installer/publish/`,
Inno output in `installer/Output/`, portable zips in `dist/` (all
gitignored; only `installer/WindowsCM.iss` is tracked).

- [ ] `dotnet publish <wpf-app> -c Release -r win-x64 --self-contained true
      /p:PublishSingleFile=true -o installer/publish` succeeds
- [ ] Same for `-r win-arm64`
- [ ] Portable zip (single exe + `LICENSE`) runs standalone from a
      non-`PATH` folder with no install, creates history on copy
- [ ] `iscc installer/WindowsCM.iss` compiles with no errors

## 2. Fresh install (per-user, no admin)

- [ ] Installs on a standard (non-admin) account with no UAC prompt
- [ ] Lands in `%LocalAppData%\Programs\WindowsCM`
- [ ] License page shows the GPL-3.0 text; installed copy exists next
      to the exe
- [ ] Start Menu shortcut `WindowsCM` exists; NO Desktop shortcut
- [ ] Autostart checkbox unchecked by default; checking it writes
      HKCU `...\Run\WindowsCM = "<exe>" --hidden`; unchecking (or the
      settings toggle) removes it
- [ ] "Launch WindowsCM now" runs the tray app; second launch hands
      off to the running instance and exits

## 3. Upgrade preserves data

Seed first: 10+ items incl. an image and a file drop, 2 pins, 2 tags,
a custom action, changed hotkey + theme, then install the new build
over the old one.

- [ ] History items, pins, tags, actions, settings all survive
- [ ] `%LocalAppData%\WindowsCM\clipboard.db` (+ images) and
      `%AppData%\WindowsCM\{settings.json,actions.json}` untouched
      (compare hashes before/after)
- [ ] Autostart choice and Run value survive the upgrade

## 4. Uninstall

- [ ] Default uninstall keeps `%LocalAppData%\WindowsCM` and
      `%AppData%\WindowsCM` (reinstall finds everything, see §3 seed)
- [ ] Removal checkbox checked deletes both data roots
- [ ] Either way the HKCU Run value is gone and `{app}` is gone

## 5. Functional smoke (seeded app, post-install)

Capture and classification (spec stories 1–12):

- [ ] Text, code (highlighted + language), image, file drop, link
      (rich preview), single character/emoji (big preview), color
      string (swatch) each classify correctly; other text falls back
- [ ] Empty/whitespace copies ignored; re-copy bumps date, no duplicate
- [ ] Password-manager and excluded-process copies never land in history
- [ ] Incognito (hotkey + tray + header) suspends capture with visible state

Pins, tags, history (stories 13–21):

- [ ] Pins survive limits and clear; 9 colored tags group without protecting
- [ ] Length/age limits evict oldest-first, protection-aware
- [ ] Clear keeps pins+tags by default; wipe-all clears everything
- [ ] Live search + remember-search + exclude-pinned/tagged filters

Copy, paste, actions (stories 22–30):

- [ ] Enter/Space copy-or-paste (swappable); Ctrl+Enter runs the type default
- [ ] Paste lands in the focused app (Ctrl+V default, Shift+Insert opt-in);
      elevated-target failure surfaces in Diagnostics, never silently
- [ ] Custom command/color/qrcode actions match by type+regex; CRUD +
      reset/restore-built-in; Ctrl+Q QR from text-like items
- [ ] Open-with-default / reveal-in-Explorer / open-in-browser

Popup, hotkeys, tray (stories 31–38):

- [ ] Ctrl+Shift+V opens at the mouse cursor on the right monitor,
      clamped to the work area at 100/150/200% DPI; Esc closes,
      focus loss hides
- [ ] Remapped open/incognito chords re-register live; occupied-hotkey
      error names the conflicting app
- [ ] Full keyboard map incl. the Alt+P pins-filter deviation; tray
      left toggles, right shows the 5-item menu; sounds + balloon
      feedback (no toast on v1)

Settings, lifecycle (stories 39–43):

- [ ] All ported settings screens apply live; Default/Compact profiles
      switch; system-follow theme default
- [ ] About/Diagnostics shows app/.NET/SQLite versions and opens
      Data/Config/Cache in Explorer
- [ ] CLI/IPC toggle/show/hide/clear/clear-all; autostart login starts
      hidden to tray; session-end cleanup never blocks logout

## 6. Sign-off

| Version | Date | Tester | Win10 20H2 | Win11 | Multi-monitor/DPI | Notes |
| ------- | ---- | ------ | ---------- | ----- | ----------------- | ----- |
|         |      |        |            |       |                   |       |
