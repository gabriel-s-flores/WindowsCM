# 11: Clipboard monitor and capture loop

**What to build:** copying anything in any app lands typed items in history:
the Win32 listener, format readers, exclusion and incognito gates, and asset
persistence wired to the classifier and store — verifiable by copying real
content and watching history grow correctly.

**Blocked by:** 09 (history store), 10 (classifier).

**Status:** implemented

- [x] Change notifications arrive via listener (no polling); activation-time sequence check only
- [x] Text, bitmap (as PNG) and file-drop captures land with correct types
- [x] Per-process exclusions skip capture; incognito suspends it with no post-toggle leak
- [x] Image bytes stored hashed with extension; file operation preserved
- [x] Copy-from-history refreshes date only when the setting says so
- [x] Clipboard ownership rules honored (copy-out immediately, STA, open-retry)

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis code-review.
194/194 xUnit green (167 prior + 27 new), 0 errors, 0 warnings (.NET 8.0.425).
Seams: `CaptureService.Capture/CopiedFromHistory/SweepOrphanImages`,
`ClipboardMonitor` (event + activation check), `FileImageAssetStore`
(save-if-absent + sweep), `ClipboardOwnership` (STA guard + open-retry),
`DibToPng` (32/24bpp BI_RGB → PNG) — store is real SQLite `:memory:`,
images land in temp dirs, OS edges faked. Win32 P/Invoke
(`Win32ClipboardReader`, `MessageOnlyClipboardListener`,
`Win32SequenceProvider`, `Win32ForegroundProcess`) compiles with no
warnings; real-clipboard behavior is manual-smoke per Testing Decisions.
Review fixes folded in: locked HGLOBAL reads for text/drop-effect, DIB
fallback `continue`, monitor sequence sync on notification (+ regression
test), `GetFileNameWithoutExtension` stem, swallow comments, `output`
rename, provider files split, listener thread join. Declined: paletted/
compressed DIB support (documented v1 limit, screenshots are 32bpp);
excluded-copy prev poisoning (Copyous prev-before-gate parity);
SaveIfAbsent hash-key enforcement (caller-owned invariant);
From32bpp/From24bpp + triple-switch duplication (small, stable);
`_lastSeen` tuple and `ClipboardPorts` naming (judgement-only).
