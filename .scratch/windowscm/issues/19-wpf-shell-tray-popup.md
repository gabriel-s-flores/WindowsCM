# 19: WPF shell (tray, popup, hotkeys, lifecycle)

**What to build:** a runnable per-user tray app: `src/WindowsCM.App` (WPF,
.NET 8, WinExe) over the Core contracts — single-instance + pipe handoff,
tray icon with the five-item menu, card-strip popup at the cursor with full
keyboard operation and copy/paste/default-action/QR activation, global
hotkeys, capture loop, autostart, session-end cleanup, and a Settings window
covering Diagnostics/About + autostart + folder shortcuts. Publishable
single-file to `installer/publish/` so `iscc` has an exe to wrap.

**Blocked by:** 15 (hotkey/tray/popup contracts), 17 (IPC/lifecycle contracts).

**Status:** resolved

- [x] Solution builds with zero errors/warnings; app starts tray-only with `--hidden`
- [x] Second launch forwards and exits; pipe toggle/show/hide/clear/clear-all live
- [x] Tray left toggles popup, right shows Open/Incognito/Clear/Settings/Exit
- [x] Popup opens at cursor DPI-clamped, hides on focus loss, Esc closes
- [x] Full keyboard map drives selection; Enter/Space copy-pastes, Ctrl+Enter runs default, Ctrl+Q shows QR
- [x] Open/incognito chords register with defaults, occupied chords surface guidance
- [x] Copying anywhere lands typed items in history (monitor wired)
- [x] Session-end cleanup runs the configured mode; autostart toggle binds the Run key
- [x] `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o installer/publish` succeeds

## Comments

Deferred to 20 (full settings screens: History/Behavior/Exclusions/Dialog/Item/Header/per-type/Shortcuts/Actions UI): the Settings window here covers Diagnostics/About + autostart + folders only. Deferred: AvalonEdit code control (plain-text fallback), wav assets + MediaPlayer wiring (silent default stands), caret-UIA placement (cursor-first v1), MSIX.

Deviation: H.NotifyIcon.WPF dropped — v2.4.1 ships no net8.0 target (net10.0 + net462 only). WinForms NotifyIcon (in-box) covers the whole tray contract: left/double-click toggle, five-item menu, balloon, icon flash.
