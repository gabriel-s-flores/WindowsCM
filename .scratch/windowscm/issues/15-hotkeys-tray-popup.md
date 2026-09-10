# 15: Hotkeys, tray and popup

**What to build:** global chords open the card-strip popup at the cursor, the
tray hosts the app with its five-item menu, and the full keyboard map drives
history — a clean rewrite informed by the prototype verdict (never a
promotion of prototype code).

**Blocked by:** 09 (history store), 11 (capture loop).

**Status:** resolved

- [x] Open/incognito chords register with defaults, remap live, and report conflicts with guidance
- [x] Tray left-click toggles the popup; right-click shows Open/Incognito/Clear/Settings/Exit
- [x] Popup opens at the cursor with DPI-aware clamping, hides on focus loss, closes on Esc
- [x] Full keyboard map works (navigation, jump, pins filter with the Alt+P deviation, type/tag cycles, delete, default-action chord)
- [x] Selection/filter/pin/delete/clear/incognito behave per the ViewModel state machine (tested over a fake store)
- [x] Cards match the verdict: Copyous density, Dark default, no vertical dead space, full width

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis self-review
(Standards + Spec). 493/493 xUnit green (392 prior + 101 new), 0 errors,
0 warnings (.NET 8.0.425, no new packages — BCL only).
Seams: `HotkeyChord` (parse/format/Win+F12/bare-key validation),
`HotkeyService` over `IHotkeyRegistrar`/`IHotkeySettings` fakes
(defaults, NOREPEAT always, unregister-before-reregister, occupied-1409
guidance, remap rollback + persist-on-success), `Win32HotkeyRegistrar`
P/Invoke, `TrayController` over popup/incognito/history/settings/exit
fakes (left toggles, double-click aliases, 5-item menu, Clear keeps
pins+tags), `PopupPlacement` (cursor +12 offset, work-area clamp,
multi-monitor origins, ToDips), `PopupKeyboardMap` (full Copyous map with
the Alt→Alt+P deviation, search-box scoping, Ctrl+Shift digit tag slots,
scroll target + swap), `PopupViewModel` over a fake `IHistoryStore`
(selection wrap, jump slots incl. 0-is-10th, live search, pins/type/tag
cycles, pin flip, delete refusing pinned unless forced, clear
keep-protected/all, incognito, Dark/Default defaults), `PopupCards`
verdict pins (250×170, Dark, full-width, no dead space) + `ItemTags`
(9 Copyous hexes). Store gained `Delete(id)` (SQLite impl + dedicated
tests) for the Delete key.
Review fixes folded in: xUnit2000 arg order, CS0414 dead field, redundant
StartsWith branch, reflection-based fake lookup replaced by a local,
menu-"entries" wording (CONTEXT.md item vocabulary).
Deferred by ticket boundary: real WPF window + H.NotifyIcon wiring +
GetCursorPos/Screen adapters (UI layer; Core is net8.0 with no WPF refs —
no UI automation in v1 per Testing Decisions); editor/menu/focus-search
dialogs behind EditItem/EditTitle/ShowActionsMenu/FocusSearch (16);
Ctrl+Q QR chord stays in the actions layer (13).

Formal /code-review 2026-09-10 (diff f6e98e2..6931c1a): Standards 0 hard + 6 smells (kept); Spec 11 findings. Fixed: hotkey IDs 0x8000/0x8001 (app range); ActivationRequest seam added (ViewModel resolves WHAT — selected id + default-action flag; shell executes HOW via orchestrator/executor). Open: WPF window/TaskbarIcon/adapters/balloon/flash/header-footer/profile-apply (shell), extra chords (accepted, 16 dialogs own them), single-WorkArea clamp, search-box caret, Delete asset leak, guidance suffix. Suite 689/689. Status → resolved.
