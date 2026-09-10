# WindowsCM — Spec v1 (paridade total Copyous no Windows)

Status: ready-for-agent

Fonte: mapa wayfinding (8/8 tickets resolved) + researchs `01`–`05` +
`CONTEXT.md` + veredito do protótipo (branch `prototype/popup-wpf`,
vencedor A · Cards). Vocabulário segue `CONTEXT.md`: **item** = domínio/UI,
**entry** = só código/DB. UI em inglês na v1.

## Problem Statement

Windows users who live in copy-paste (developers, writers, support) have no
clipboard manager that treats every content kind as first-class: the native
`Win+V` history is text-and-image only, with no pins, no tags, no per-type
actions, no global hotkeys worth using, and no real customization. GNOME
users solved this with Copyous; on Windows the gap is open.

## Solution

A native WPF (.NET 8) clipboard manager for Win10 20H2+/Win11 with full
Copyous parity plus a system-tray home: it captures text, code, images,
files, links, characters and colors; pins favorites; groups with 9 colored
tags; runs customizable per-type actions; opens at the mouse cursor via
global hotkeys; persists everything in local SQLite; and exposes tray menu,
CLI and local IPC mirroring the Copyous DBus API.

## User Stories

### Capture & classification

1. As a user, I want everything I copy (text, code, image, files) captured
   automatically, so that my history builds itself.
2. As a user, I want links detected when clipboard text starts with http and
   is a valid URI, so that URLs become rich link items.
3. As a user, I want single characters/emoji detected as character items
   (up to max-characters), so that symbols are previewed big.
4. As a user, I want color strings (named, hex, rgb, hsl and friends)
   detected as color items, so that I get swatches not text.
5. As a user, I want code auto-detected with its language, so that snippets
   get highlighting instead of plain text.
6. As a user, I want anything else to fall back to plain text, so that
   classification never loses content.
7. As a user, I want images probed before files before text, so that the
   richest representation wins.
8. As a user, I want empty/whitespace-only copies ignored, so that noise
   never enters history.
9. As a user, I want duplicate copies deduplicated (bumping date), so that
   re-copying does not spam history.
10. As a user, I want copies from password managers ignored, so that secrets
    never land in history.
11. As a user, I want per-process exclusions, so that selected apps never feed
    history (Windows map of wmclass-exclusions).
12. As a user, I want incognito mode (global hotkey + tray + header toggle),
    so that I can pause capture with visible state.

### Pins, tags, history

13. As a user, I want to pin favorite items, so that limits and clears never
    remove them.
14. As a user, I want 9 colored tags orthogonal to pins, so that I can group
    items without protecting them.
15. As a user, I want history bounded by length (10–500, default 50) and age
    (minutes, 0 = unlimited), so that the DB stays small.
16. As a user, I want pinned/tagged items protected from limits by default
    (toggles), so that favorites survive rotation.
17. As a user, I want clear-history keeping pins+tags by default (or wipe
    everything), so that cleanup is one click and safe.
18. As a user, I want auto-cleanup on restart/logout/shutdown per my
    clipboard-history setting, so that ephemerality is enforced.
19. As a user, I want live search with optional remember-search, so that the
    popup recalls context or starts clean per my taste.
20. As a user, I want exclude-pinned / exclude-tagged search filters, so that
    daily triage hides favorites.
21. As a user, I want copying an item back to refresh its date optionally, so
    that reused items resurface.

### Copy, paste, actions

22. As a user, I want Enter/Space to copy-or-paste (swappable), so that muscle
    memory from Copyous transfers.
23. As a user, I want Ctrl+Enter to run the default action for the type, so
    that files open and links launch with one chord.
24. As a user, I want paste injected into the focused app (Ctrl+V default,
    Shift+Insert opt-in), so that copy-then-paste is one flow.
25. As a user, I want per-type default actions (paste-as-path for files,
    open-with-browser for links…), so that each kind does the smart thing.
26. As a user, I want custom actions (command / color-convert / qrcode) matched
    by type + regex with shortcuts and submenus, so that the manager extends
    to my workflow.
27. As a user, I want CRUD + reset/restore-built-in for actions, so that
    experimenting is safe.
28. As a user, I want QR codes from text-like items (Ctrl+Q), so that I can
    move strings to my phone.
29. As a user, I want color conversions (rgb/hex/hsl/oklch…), so that palettes
    translate one click.
30. As a user, I want open-with-default / reveal-in-Explorer / open-in-browser,
    so that images, files and links escape the popup correctly.

### Popup, hotkeys, tray

31. As a user, I want the popup at my mouse cursor via Ctrl+Shift+V (and
    incognito via Ctrl+Shift+Alt+V), both remappable, so that history is one
    chord away anywhere.
32. As a user, I want the card-strip popup (Copyous density, Dark theme,
    full-width, no dead whitespace), so that 8 kinds scan at a glance.
33. As a user, I want Default and Compact profiles (horizontal/vertical,
    sizes, auto-hide, header), so that the popup fits taste and screen.
34. As a user, I want full keyboard operation (arrows/Tab/Home/End, jump
    Ctrl+0..9, Alt+P pins filter, Ctrl+Tab type cycle, Ctrl+` tag cycle,
    Delete, Esc), so that I never touch the mouse.
35. As a user, I want the popup to hide on focus loss and close on Esc, so
    that it never lingers.
36. As a user, I want tray left-click to toggle the popup and right-click for
    Open/Incognito/Clear/Settings/Exit, so that the app has a visible home.
37. As a user, I want link previews (title/desc/image, cached, offline =
    none) and code highlighting with plain-text fallback, so that items read
    rich but never break.
38. As a user, I want sounds (9 options + dB volume) and balloon feedback with
    icon flash instead of wiggle, so that copies confirm without nagging.

### Settings, lifecycle, distribution

39. As a user, I want all Copyous settings ported (History/Feedback/Behavior/
    Exclusions, Dialog/Item/Header/per-type/Theme, Shortcuts, Actions), minus
    GNOME-only cuts (Yaru, indicator-display, WM_CLASS), so that customization
    has parity.
40. As a user, I want an About/Diagnostics page (library versions + open
    Data/Config/Cache in Explorer), so that troubleshooting is self-serve.
41. As a user, I want single-instance with second-launch handoff, autostart
    opt-in (Run + --hidden), and CLI/IPC (toggle/show/hide/clear), so that
    the app behaves like a Windows citizen and scripts can drive it.
42. As a user, I want per-user Inno Setup install (no admin) plus portable
    single-file zip, upgrade preserving data, and uninstall keeping data
    unless I opt out, so that install/uninstall never surprises me.
43. As a user, I want GPL-3.0-or-later with SPDX headers and an About dialog
    crediting Copyous/Pano and the MIT libraries, so that lineage is honored.

## Implementation Decisions

- Stack: C# WPF on .NET 8, Windows-only, Win10 20H2+/Win11. No WebView/
  Chromium/Electron/Tauri (research 02 confirmed the native gaps).
- Clipboard core: `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` on the
  WPF HWND (message-only window fallback); `GetClipboardSequenceNumber` only
  as punctual check, never polling. Formats: Unicode text, DIB/V5 preferred
  for bitmaps (persist PNG via encoder), `CF_HDROP` with file count via
  `DragQueryFile` and cut-vs-copy via `Preferred DropEffect`; `CF_HTML`
  stored opaque and rewritten verbatim in v1. Clipboard handles copied
  immediately, never freed/locked; STA thread; open-retry with backoff.
- Classifier (pure): probe Image > File > Text; text order Link (http prefix
  + valid URI) → Character (`Intl`-style grapheme count ≤ max-characters,
  default 1) → Color (ported CSS Color 4 grammar) → Code (highlight
  auto-detect on 10k slice, relevance/density threshold) → Text. MD5 dedup;
  own-copy suppression recorded before the save gate so incognito never leaks.
- History store on `Microsoft.Data.Sqlite` (EF Core rejected): one `clipboard`
  table (`id`, `type`, `content`, `pinned`, `tag`, `datetime`, `metadata`
  JSON, `title`) with `UNIQUE(type,content)` plus a version table (v2); WAL
  mode; `INTEGER`/`TEXT` affinities; indexes for date-desc and pin/tag.
  Upsert bumps datetime on conflict (never replace-row, it renumbers id);
  property edits conflicting on unique return the conflicting id unapplied.
  Clear keeps `pinned OR tagged` unless wipe-all; oldest-eviction combines
  over-count and older-than-age, both protection-aware. Corrupt metadata
  degrades to null, never drops the item. SQLite for production, `:memory:`
  for tests only, JSON backend dropped. Path override order: env var, then
  setting, then LocalAppData default. `Sync Primary` permanently N/A (Win32
  has a single clipboard).
- Item assets: image bytes under LocalAppData images dir keyed by content
  hash with extension; link thumbnails under a cache dir keyed by URL hash;
  files deleted only after DB commit plus orphan sweep at startup.
- Search: case-insensitive LIKE with escape plus title fallback; remember-
  search persisted as session setting; exclude/protect flags compiled into
  query predicates; copy-from-history optionally refreshes datetime.
- Paste: capture target at hotkey time, hide UI, assert foreground, inject
  via SendInput; `Ctrl+V` default, `Shift+Insert` opt-in; terminal
  auto-heuristics deferred (manual fallback); post-focus delay is a measured
  tunable, not a ported constant; elevated-target UIPI failures surface in
  Diagnostics, never silently.
- Actions engine: `actions.json` under Roaming AppData with 1:1 schema
  (command/color/qrcode kinds, submenus, per-type defaults, shortcuts);
  matching = type subset plus .NET regex (documented Unicode-semantics
  difference vs JS, 2s match timeout, bad-pattern = no-match anti-ReDoS);
  commands run via shell with content on stdin, 30s timeout, trimmed stdout,
  `$1` rewritten to the Windows placeholder. Ported defaults: shell-execute
  open, Explorer reveal-select, browser open, paste-as-path (strip `file://`
  + URI-unescape). QR via QRCoder; color conversions reuse the ported parser;
  `Ctrl+Q` default.
- Incognito: in-memory only (not persisted); capture gate honors exclusions,
  sensitivity hints, and incognito in that order.
- Janitor: clear-all vs keep-protected; session-end cleanup under one second,
  never cancel logout; CLI `--clear` (protected kept) / `--clear-all`.
- Hotkeys: per-HWND registration with app-range IDs, `NOREPEAT` always,
  explicit unregister-before-reregister; occupied-hotkey errors surface with
  which-app guidance; `Win+V`-style OS-reserved combos excluded by rule.
  Defaults open/incognito remappable and persisted in user settings with
  runtime re-registration. Internal map mirrors Copyous with the single
  deviation toggle-pinned `Alt` → `Alt+P` (WPF reserves bare Alt); text-entry
  keys scoped out of the search box; scroll cycles type, Ctrl+scroll tag,
  both swappable.
- Tray on H.NotifyIcon.WPF: left toggles popup, right shows the five-item
  menu; double-click aliases single; balloon tips for notifications (real
  toast blocked until an AUMID-holding installer exists); copy feedback =
  popup-scale plus 3×65ms icon flash.
- Popup (from prototype verdict, variant A): frameless topmost card strip at
  Copyous density, Dark default with Light/HighContrast/system-follow,
  Default profile first-run (Compact available), orientation/position/size/
  margins/search-visibility/scrollbar per settings; shared header (settings,
  incognito, search pill, pins filter, Clear) and footer in vertical mode;
  cursor-first placement with per-monitor DPI transform clamped to the work
  area (caret-UIA mode deferred to post-spec v2); rows fill horizontal width
  with no vertical dead space.
- Theming: Default/Custom (+dark/light/high-contrast), system-follow
  default, four custom colors over Copyous default fallbacks; Yaru dropped.
- Settings UI mirrors the ported prefs: History shows SQLite only (Memory is
  debug-only), `.db` file picker, length/age ranges, three end-of-session
  modes; per-type screens keep all Copyous options (file thumbnails via
  Shell providers); Shortcuts screens cover persisted plus hardcoded maps;
  Actions CRUD with per-type defaults; About/Diagnostics with versions and
  folder shortcuts (Explorer, not XDG).
- Previews: singleton HttpClient (5s timeout, product User-Agent,
  header-first reads, cancellation), HTML-only metadata via AngleSharp,
  regex exclusions, URL-hash image cache; code via read-only AvalonEdit with
  plain-text fallback (hljs language map covered by spike).
- Sounds: eight converted wav assets played via MediaPlayer with
  exponential dB-to-gain mapping; richer mixer only if audibility spikes
  demand it.
- IPC: per-user named pipe with current-user-only ACL and single-line
  text protocol (toggle/show/hide/clear/clear-all); CLI mirrors it;
  second instance forwards and exits.
- Lifecycle: named mutex (per-user qualified) + pipe handoff; opt-in Run-key
  autostart with hidden flag; unpackaged-first (MSIX/Store explicitly
  post-spec).
- Packaging: self-contained single-file portable zip plus per-user Inno
  installer (Start Menu shortcut, no Desktop icon by default, LICENSE
  bundled); upgrades preserve user data; uninstall preserves data unless the
  removal checkbox is set.
- Compliance: GPL-3.0-or-later, SPDX identifiers in sources, About credits
  for Copyous/Pano and bundled MIT libraries, public repo from day one.
- UI language English v1; all domain naming follows `CONTEXT.md`.

## Testing Decisions

- A good test asserts externally visible behavior at a seam, never
  implementation details: given inputs in, assert store/query/IPC/UI-state
  out. Time-based behavior uses injected tunables, never real sleeps; no
  clock-dependent flakes.
- Pure logic (classifier incl. thresholds, color parser, action matcher,
  path rewrite, tag/pin predicates) tested directly with zero mocks.
- Store tested against real SQLite `:memory:` (upsert/dedup, conflict-id
  returns, clear/protect matrix, eviction math, search filters, corrupt-
  metadata degradation, migration v1→v2).
- OS boundaries behind fakes: monitor event source, clipboard reader/writer,
  paste injector (assert chord + target handling), process runner (assert
  argv/stdin/timeout mapping), settings file in temp dirs, pipe server with
  unique names per test, preview HTTP stub, sound player stub.
- Popup ViewModel tested over a fake store: selection/filter/pin/delete/
  clear/incognito/theme state machine incl. empty-filter and first/last
  edges. No UI automation in v1.
- Manual smoke checklist covers what automation cannot: real Win32 capture
  across apps, elevated-target paste, hotkey conflicts, tray gestures,
  popup placement on multi-monitor/DPI, installer upgrade/uninstall matrix,
  sounds audibility.
- Prior art: none in-repo (greenfield). Oracles are the Copyous behavior
  inventory (research 01), the UI verdict (prototype branch), and the five
  decision records under `.scratch/windowscm/research/`.

## Out of Scope

Cloud sync/multi-device; third-party plugins or extension store; MSIX/Store
packaging day one; macOS/Linux ports; caret-UIA text-cursor mode (cursor-
first v1, caret is post-spec v2); real toast notifications (balloon v1,
blocked on installer AUMID); UI languages beyond English; JSON/Memory
production backends; automatic terminal paste heuristics (manual opt-in);
virtual-file (`CFSTR_FILEDESCRIPTOR`) clipboard items; richer audio mixing
beyond MediaPlayer unless spikes demand it.

## Further Notes

- Decision records live under `.scratch/windowscm/research/` (01 parity
  inventory with file:line evidence, 02 clipboard core, 03 hotkeys/tray/
  popup, 04 persistence/search, 05 actions/previews); map at
  `.scratch/windowscm/map.md`; prototype (losing variants included) on
  branch `prototype/popup-wpf` — run with `dotnet run` after installing the
  .NET 8 SDK.
- Known spikes for the ticket phase, not this spec: hljs→AvalonEdit language
  map, wav audibility, paste delay/terminal measurement, strict-mode DDL and
  datetime literal format, edit-merge variant choice.
- Suggested build order for tickets: store+classifier → monitor/paste →
  actions/previews → hotkeys/tray/popup → settings → IPC/lifecycle →
  installer/compliance, blockers-first.
