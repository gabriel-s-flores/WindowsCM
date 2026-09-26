# Second stability and performance audit

Status: resolved
Type: fix
Base: main (after PR #10 was merged)

## Request

"Do one more check and audit focused on performance and stability: test, validate and find bugs and loose ends. The goal is for the app to be as stable and performant as possible, with no crashes or slowness."

## How it was done

- Four code audits in parallel: lifecycle/threads, popup/converters, capture/paste/history, transfer/previews/actions/settings. Each finding was checked in the code before it went on the list. Some of them were also reproduced with a test or measured.
- Throwaway microbenchmarks (outside the repo) on the Core hot paths with pathological inputs: 2 MB texts, minified JSON, millions of lines, ZWJ emoji, forged DIB headers, histories with large CF_HTML and 4K/8K images.
- Every fix in Core got a test that failed before. The WPF layer does not run on Linux: it was reviewed in the code and is validated by the Windows CI (build, tests, real-app smoke and scroll smoke).

## Findings and fixes

### Crashes and hangs

| # | Where | Problem | Fix |
|---|---|---|---|
| 1 | Popup | Clicking the text of a code card threw an exception: the click lands on a `Run` and `VisualTreeHelper.GetParent` does not accept a `Run`. The 6th click in 10 s ended the app. | `VisualTreeWalk` climbs content elements through the logical tree. |
| 2 | Popup | Alt+F4 closed the popup for good; the next hotkey threw an exception on the closed window. | The popups hide on `WM_CLOSE` and only close on exit. |
| 3 | Popup/SQLite | Pasting a line over 50 KB into the search made the `LIKE` fail ("pattern too complex") on every refresh; with "remember search", the popup no longer opened. | Search capped at 1,000 characters; the store uses `instr` for queries of that size. |
| 4 | Popup | A paste that failed before hiding the popup left `_isActivating` set: the compact menu stayed on top, ignoring clicks and the loss of focus. | The flag is cleared when the activation finishes; the failures are logged and explained in a balloon. |
| 5 | Startup | Corrupt database, custom location unavailable (drive not mounted) or locked file: the app died on every startup, with no icon or message. | `HistoryStoreOpener`: keeps the damaged file and recreates it; uses the default location; as a last resort runs in memory. Each case has a localized balloon. |
| 6 | Tray | Exceptions in the icon's handlers (WinForms) showed WinForms' "unhandled exception" dialog, whose Quit exited without cleanup or a log. | `Application.ThreadException` registered and subject to the same burst policy. |
| 7 | Settings | A failure to save on close left the closed window referenced; "Settings" threw an exception until restart. | The reference is cleared first; saving and re-applying are guarded. |
| 8 | Instance | With an elevated instance running, the second launch got stuck on the mutex ACL. | It now counts as "another instance is the primary". |
| 9 | IPC | The client waited for the reply forever if the primary's UI hung; a silent client held the single-instance pipe. | A deadline on the read on both sides. |
| 10 | Clipboard | `CF_UNICODETEXT` without a terminator was read past the block (garbage or an access violation). | Decoding bounded by `GlobalSize`. |
| 11 | Clipboard | `DragQueryFile(i)` is O(n) per file, called twice per file with the clipboard open: tens of seconds for 50,000 files, with every app unable to copy/paste. | `DROPFILES` read in one pass; an odd path no longer drops the whole copy. |
| 12 | Clipboard | A 52-byte DIB declaring 20000×20000 allocated 1.5 GB before failing; some headers overflowed `int`. | Sizes validated in 64 bits before allocating; a limit of 8192×8192 (also for PNG). |
| 13 | Image paste | Decoding on the UI thread: 4K = 346 ms/255 MB, 8K = 1.15 s/1 GB. | Off the UI thread, with exact-size buffers: 4K = 130 ms/64 MB, 8K = 0.5 s/256 MB. |
| 14 | Actions | The timeout only started after writing the item to stdin: a command that does not read its input hung forever. If the command exited without reading, the broken pipe lost the result. | Input fed in parallel with the timed wait; output capped. |
| 15 | Transfer | Whole uploads in memory (a 1.5 GB video ≈ 3.5 GB in the process), with no deadline and no connection limit. | Multipart written straight to disk; 30 s idle timeout; 16 connections; text up to 16 MB. |
| 16 | Previews | The link to an ISO or a live stream was downloaded whole into memory, on every popup open. | Reads only HTML/image/JSON, up to 1 MB/5 MB, with a deadline of its own for the body; failures are retried only after 30 min. |
| 17 | History | Dates were written with the Windows culture. With a `.` time separator (fi-FI, da-DK or a custom setting), every read failed and the popup never opened. In another calendar (th-TH, fa-IR), the years were centuries off. | Invariant writes; old rows re-read with the culture that wrote them and rewritten on open. |

### Data loss and wrong behavior

| # | Problem | Fix |
|---|---|---|
| 17b | Dragging the "history limit" slider from 100 to 10 and back deleted 90 items (evict at every step). | Evict only when Settings closes; live updates batched (200 ms). |
| 18 | The hex color and extensions boxes were recreated on every keystroke, preventing typing. | The panel is rebuilt only when the color scheme changes. |
| 19 | A `settings.json` locked for a moment at logon loaded the defaults, and the next save overwrote everything. | Reads with retries. |
| 20 | With different orders in the two popups, a global refresh reordered the shared model: clicking card i in the large popup pasted another item. | Only the open popup is refreshed, in its own order. |
| 21 | With incognito mode active and the normal history view open, paste/pin/edit/delete acted on the incognito session by id, and the paste recording (date, echo suppression) landed on another item. | The app, the view model and the paste recording act on the displayed history. |
| 22 | Pasting an image captured in incognito mode said "file missing". | Reads the incognito session's folder. |
| 23 | Re-copying an item wiped its title and metadata (preview); the new preview fetch also overwrote a title given by the user. | Title preserved; metadata merged (enrichments stay, old CF_HTML goes if the new copy has none); a link that already has a preview is not fetched again. |
| 24 | A row with an unreadable date made every read fail; the popup did not open. | The row is skipped. |
| 25 | A capture whose write failed was marked as seen; the re-copy was dropped. | Marked only after writing. |
| 26 | "Clear cache" broke link previews until restart. | The folder is recreated on write. |
| 27 | Received file names with `:` became a hidden NTFS stream; `? *` failed the upload. | Names sanitized; `CreateNew` avoids overwriting. |
| 28 | A double-click on the tray icon opened and closed the popup. | Only a single click toggles. |
| 29 | Alt shortcuts (Alt+P etc.) never worked (`Key.System`); in the compact menu, typing with AltGr (ś, ć) opened Settings or asked to clear. | `SystemKey`; shortcuts only with Alt alone; the menu shows the real pin shortcut (Ctrl+S). |
| 30b | A preview finishing after incognito mode was toggled wrote to the item with the same id in the other session. | The write is discarded if the session changed. |

### Security (loose ends)

| # | Problem | Fix |
|---|---|---|
| 30 | `/api/upload` accepted text/files from any device on the network and put them straight on the clipboard. | A 128-bit key per run, only in the QR. See the revision of ADR 0003. |
| 31 | Server listening on the network from startup (firewall alert for everyone). | Starts on first use. |
| 32 | Shares with a 32-bit token and no expiry. | 128 bits, 24 h. |

### Performance

- JSON validation of every row on every read (opening the popup with 30 browser/VS Code copies): 79 → 23 ms via `json_valid`. CF_HTML is now stored without 6-character escapes (about half the size).
- Link card: 21 JSON parses per realization → 1. Link/character converters return at once for other types.
- Image and link preview thumbnails decoded off the UI thread (before: 50–200 ms per 4K card while scrolling).
- "Files" card with thousands of paths: counted, not split; only the first 10 detailed.
- Search with a 120 ms debounce, applied before any key that acts on the list.
- Theme rebuilt only when the scheme changes (before, on every Windows preference notification).
- Watchdog without leaking a kernel handle per second; copy feedback without blocking the capture thread.
- Emoji: Direct2D/DirectWrite objects released at once, bounded cache, up to 24 emoji per tile. Favicons: bounded cache and retries only after 15 min.
- Images of removed items deleted while running (at most every 15 min), atomic writes, orphaned incognito folders removed at startup. The link image cache is swept at startup. The sweeps only run against the real history opened normally (the images folder is shared: with the default location standing in for a missing drive, the sweep would delete the real history's images) and read the image rows straight from the database.

## Out of scope (recorded for later)

- **Command injection in custom actions**: regex groups (clipboard text) go raw into the `cmd.exe` command line. It only affects command actions created by the user with capture groups; the built-in ones are not affected. The fix needs safe escaping for `cmd.exe`, validated on Windows.
- **Canceled shutdown**: WPF ends the app on `WM_QUERYENDSESSION`, and the end-of-session cleanup also runs there. A canceled shutdown (or the installer's Restart Manager) deletes the unpinned history.
- **Incognito × pending capture race**: leaving incognito mode up to 0.5 s after a copy can write it to the normal history.
- **A second launch during startup** can show "not running" if the first one takes more than 2 s to open the pipe (a limit documented in research).
- **`VirtualizationMode=Recycling`** is still not applied: it changes the cards' lifecycle and needs to be measured on Windows.
- Every copied link is fetched automatically, including intranet ones (single-use links can be "used up"); link exclusions are the current control.

## Verification

- Core: 1345 tests pass on Linux; the failures are the same as on the base (`C:\` paths, the installer contract and a pipe test that only behaves this way outside Windows), which pass on the Windows CI. More than 100 new tests cover the fixes.
- Independent review of the whole diff (Core and WPF) before the PR; the problems it found were fixed. The most serious: the image sweep against a stand-in history, the culture-dependent dates and the paste recording during incognito mode.
- Build of the whole solution (includes the WPF app, `-p:EnableWindowsTargeting=true`) with no warnings.
- Windows CI on PR #11 (2-vCPU runner, no GPU): build, tests, real-app smoke and scroll smoke, all green. The first run caught three new tests that only failed on Windows; one of them revealed that the pipe write also needed a deadline (fixed in the production code).
  - Other apps' clipboard: 0 failures in 300 copies. The history stays at 100 items and nothing was written to the error log.
  - Memory after 300 copies: private 141–147 MB (before ~190–210), working set ~260 MB (before ~300), managed heap after GC 24–28 MB (before ~60), stable.
  - Popup: p50 101 ms via pipe and 144 ms idle. Hotkey → popup: 245–661 ms. Auto-paste into Notepad: in all 4 scenarios.
  - Scrolling at 1920×1080 with 130 items: UI thread round trip p95 34 ms (wheel), 3 ms (touchpad), 1 ms (burst); no stall of 1 s or more.
- `dist/`: the portable version (`WindowsCM-portable/` + `.zip`) was generated by a cross-publish on Linux. The installer requires Windows + Inno Setup and is produced by the CI release job on every green push to `main`.
