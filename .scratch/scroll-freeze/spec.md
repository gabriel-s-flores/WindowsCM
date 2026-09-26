# Popup freezes and closes when scrolling a full list

Status: resolved
Type: fix

## Report

"When I scroll a full list the app simply stops responding and crashes" —
it persisted after the performance and stability audit. Screenshot: large
popup (horizontal, 1880 px), ~75% of the list scrolled, a whited-out
"Not Responding" window.

## Reproduction (`scripts/smoke/scroll-smoke.ps1`)

No test scrolled the popup. The scroll smoke (real app, Windows CI,
1920×1080) fills the history (100 items: long prompts, code, screenshots,
real links, files, emoji, colors), opens the popup and spins the wheel over
the cards ("flicked" wheel, touchpad, bursts), measuring the round trip of a
message through the UI thread (`WM_NULL`). On a hang, it saves the managed
stacks (`dotnet-stack`); on a crash, it summarizes the runtime dump
(`dotnet-dump`).

- History with local files only: no hang (3 runs).
- A file item on a share that does not respond
  (`\\10.255.255.1\share\report.pdf`), deep in the history: **the popup
  stopped responding after 4.7 s of scrolling, for ~20 s**, the moment the
  card appeared. UI thread stack:
  `File.Exists` ← `ImagePreviewVisibilityConverter.Convert` ←
  `BindingExpression.Activate` ← `FrameworkTemplate.LoadContent` ←
  `VirtualizingStackPanel.MeasureChild`.

## Cause

The card converters queried the disk and the Shell **on the UI thread**, on
every card realization: `File.Exists`/`FileInfo` (thumbnail, image/file
visibility, size, details), Shell thumbnail extraction, `SHGetFileInfo` on
the real path (icon), audio/video tags through the property store — from 9
to ~20 calls per file card. The `FileIconConverter` called
`Directory.Exists` even for text cards (the first line of the text). On a
path that responds slowly — an offline share, WSL (`\\wsl.localhost\…`) with
the distro stopped, a sleeping disk, a removed USB stick — each call blocks
for seconds. With standard virtualization, the card is re-created every time
it comes back on screen, so scrolling back and forth repeats the block:
"Not Responding" and, on closing, the "crash".

Not confirmed on the user's machine (that needs the log/Event Viewer): it
is the reproduced mechanism that matches the report (a hang when scrolling
to old items).

## Solution

- `BackgroundProbeCache` (Core, tested): a lookup never runs the probe on
  the caller's thread. A miss or a stale entry (30 s) schedules one probe per
  key; the stale value keeps being served; `Updated` only when the answer
  changes.
- `CardFileFacts` (App): existence, folder, size, thumbnail (decoded image
  or Shell), file icon, audio cover/tags and video badge, probed on a
  dedicated STA worker (the Shell handlers want an STA; from an MTA thread,
  COM would go back to the main STA — the UI's).
- The converters only read `CardFileFacts`. While probing, the card shows
  the extension's generic icon (`GetExtensionIcon`, registry only) and the
  name. Both popups do a single `Items.Refresh()` per burst of answers. A
  language switch clears the cache (formatted sizes/durations).

Cost per scroll frame (CPU profile of the UI thread, 20 s of scrolling):

- `DropShadowEffect` moved from the root border to an empty sibling behind
  it. With the effect on an ancestor, every `TranslatePoint` of the mouse
  synchronization (one per frame) computed `VisualDescendantBounds` of the
  whole popup: 7.2 s of 20 s.
- Syntax highlighting only for code cards (`CodeContentConverter`); before,
  it created hundreds of `Run`s per text card, in a collapsed `TextBlock`.

Result: UI thread managed CPU 9.5 s → 5.2 s; mouse synchronization
7.2 s → 2.3 s; highlighting 0.93 s → 0.05 s. UI round trip while scrolling:
p95 55 → 26 ms (wheel), 13 → 1 ms (burst).

## Logs for the next freeze

- `UiHangWatchdog` (Core, tested): a background thread posts a no-op to the
  UI every 1 s; if it does not run within 5 s, it writes to
  `%LOCALAPPDATA%\WindowsCM\logs\windowscm.log`
  `[ui-hang] the UI thread has not answered for 5 s (private … MB, …
  handles, … threads, GDI …, USER …)` and, when the UI comes back, the total
  duration.
- `ErrorLog.Note` for events that are not exceptions.

## Out of scope

- `VirtualizationMode=Recycling`: would cut template creation (2.4 s of
  20 s in the profile), but changes the cards' lifecycle; measure first.
- The process's total CPU on the runner (~3.5 cores) is software rendering
  (no GPU); not measured on real hardware.
- `LinkPreviewImageConverter` still decodes the preview image (local cache)
  on the UI thread on every realization.

## Verification

- Core: 1232 tests pass on Linux; the 8 failures are the same as on `main`
  (`C:\` paths, pipes) and pass on the Windows CI.
- WPF app build (`-p:EnableWindowsTargeting=true`) with no warnings.
- Scroll smoke on CI: red on `main` with the unreachable share (hung for
  20 s), green with the fix.
