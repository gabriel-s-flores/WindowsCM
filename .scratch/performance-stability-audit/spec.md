# Performance and stability audit

Status: resolved
Type: fix

## Report

The app crashed many times, especially with many items in the history, and
was slow.

## Findings (in order of severity)

### Crashes

1. **An exception on the clipboard listener thread kills the process.**
   `MessageOnlyClipboardListener.WndProc` is a native callback (reverse
   P/Invoke) that raises `ClipboardChanged` → `ClipboardMonitor` →
   `CaptureService` → SQLite/image IO, and then the `App`'s copy feedback.
   Any exception there (SQLite busy, disk full, corrupt PNG,
   `Dispatcher.Invoke` failing) crosses the native boundary and takes the
   process down without warning.
2. **Incognito mode store without synchronization.** The `:memory:`
   `SqliteHistoryStore` created by the `IncognitoSessionCoordinator` does not
   go through the `LockedHistoryStore`: it is used at the same time by the
   listener (capture), the UI (popup), the pool (link previews) and the pipe.
   `SqliteConnection` is not thread-safe, and `SetIncognito(false)` disposes
   of the store while another thread is still using it → native corruption /
   `ObjectDisposedException`.
3. **No global safety net.** No handler for
   `DispatcherUnhandledException`, `AppDomain.UnhandledException` or
   `TaskScheduler.UnobservedTaskException`, and no log: any error in a UI
   handler closes the app and leaves no trace for diagnosis.
4. **`CaptureService` shared state without a lock.** `_lastSeen` is read and
   written by the listener (capture) and by the UI (`CopiedFromHistory`).
5. **Unguarded `Clipboard.SetText`** in the "Copy" action (clipboard held by
   another app → `COMException`).
6. **Listener disposed on the wrong thread.** `PostQuitMessage` was called on
   the UI thread, so the listener's loop never exited and the `Join` waited
   2 s on every shutdown.

### Slowness

7. **History limit applied only at startup.** `Evict` ran only at startup
   and when Settings closed; during the session the history grew without
   bound (and orphaned images stayed on disk until restart).
8. **Full history scan on hot paths.** `List()` (every row + a JSON parse
   per row) on every copy (feedback), on every paste (`PasteOrchestrator`,
   `CopiedFromHistory`, `ActivateAsync`) and on every popup open
   (`EnsureLinkPreviewsForRecentItems`).
9. **Text preview proportional to the whole content.** `GetPreviewText`,
   `GetTitle` and `CodeSyntaxTokenizer.Tokenize` did a `Split` of the whole
   content to use 1–8 lines; `DetectLanguage` ran a regex with `.*` and no
   timeout over the whole text (backtracking on large minified texts). All
   on the UI thread, for every realized card. Giant lines (minified JSON/JS)
   went whole into the `TextBlock`.
10. **Thumbnails decoded on every card realization.** The
    `ImageThumbConverter` decoded the PNG from disk on the UI thread with no
    cache; the Shell thumbnail and media metadata caches grew without bound.
11. **Refresh storm.** Every downloaded favicon did an `Items.Refresh()` and
    every completed link preview reloaded the model and both windows.
12. **On-disk size of every file in a multi-file copy.**
    `FileDisplayHelper.GetFileDetails` did `File.Exists` + `FileInfo` for
    every path of a "Files" item (4 converters per card, UI thread), even
    though the size only shows for a single file. Copying 2000 files in
    Explorer = ~16,000 syscalls per card; on network paths, seconds-long
    freezes.

## Plan / solution

- `IHistoryStore.GetById` / `GetLatest` (indexed queries, with a default
  implementation for fakes) replace `List()` on the hot paths.
- `IncognitoSessionCoordinator` serializes every forwarded call under the
  same lock that swaps/disposes of the ephemeral store.
- `CaptureService` serializes capture/`CopiedFromHistory`/toggle and applies
  the history limits (`CaptureOptions.HistoryMaxItems`, etc.) on every stored
  item. Image files of removed items are still cleaned up by the startup
  orphan sweep (cleaning up at runtime would race with the incognito session
  swap and could delete images of the wrong session). Changes to the limit
  slider reach capture without having to close Settings.
- `ClipboardMonitor` never lets a read/capture failure escape
  (`CaptureFailed` event); the listener's `WndProc` also catches everything.
- `ErrorLog` (Core) writes to `%LOCALAPPDATA%\WindowsCM\logs\windowscm.log`
  with size-based rotation, without clipboard content. `UnhandledErrorPolicy`
  keeps the app alive on isolated UI errors, but lets it exit on a burst
  (avoids an infinite error-per-frame loop).
- Bounded previews: `TextPreview` walks only the start of the text and clips
  long lines; `DetectLanguage` uses a 4000-character slice with a compiled
  regex with a timeout.
- `LruCache` (Core) bounds the thumbnail and metadata caches. Image
  thumbnails are now cached and prewarmed in the background (new captures,
  and the history's image items at startup).
- Coalesced refresh: link previews and favicons schedule a single refresh
  per ~150–250 ms window.
- `GetFileDetails` only queries the disk when the item has a single file.
- `Clipboard.SetText` in the "Copy" action is guarded, with a localized
  balloon (`TrayCopyFailedBalloon`, PT/EN) instead of failing silently.
- The listener shuts itself down via a `WM_CLOSE` posted to its own window.

## Out of scope (findings for future tickets)

- **Ambiguous IDs when viewing the normal history with incognito mode
  active.** Pinning, deleting, editing and pasting from that view resolve the
  id in the *active* store (the incognito one), so they can act on another
  item with the same id. It is neither a crash nor slowness, but it is data
  loss — it deserves its own ticket.
- **Re-copying an existing item wipes its title/metadata.** The
  `AddOrUpdate` "bump" overwrites `title`/`metadata` with those of the new
  capture (null).
- **The Shell thumbnail (videos, PDFs) is still extracted on the UI thread**
  on first display; prewarming it would require a dedicated STA worker.
- **`VirtualizationMode=Recycling`** on the lists would reduce container
  creation while scrolling; not applied without being able to validate it
  visually on Windows.
- **`EmojiService`** creates a Direct2D factory per emoji and does not
  release the COM objects (bounded by the per-unique-emoji cache).

## Verification

- Core tests (new + existing) in `dotnet test`: 1136 pass; the remaining
  failures are exactly those of the baseline (before the changes) and only
  occur outside Windows (`C:\` paths, named pipes) — the Windows CI covers
  them.
- Build of the whole solution, including the WPF app
  (`dotnet build -p:EnableWindowsTargeting=true`), with no warnings.
- Windows CI (build + tests) on the PR. `scripts/build-dist.ps1` (portable +
  installer) requires Windows + Inno Setup: it runs in the CI release job on
  every green push to `main`.
