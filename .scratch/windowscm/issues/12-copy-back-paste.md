# 12: Copy-back and paste

**What to build:** choosing an item puts it back on the clipboard and pastes
it into the previously focused app, with the chord, fallback sequence and
swap setting from the spec — failures against elevated targets surface
instead of vanishing.

**Blocked by:** 09 (history store).

**Status:** resolved

- [x] Text kinds write text; images write bitmaps; file lists write drop data forcing copy
- [x] Paste injects the default chord, with the legacy sequence as opt-in setting
- [x] Paste target is captured at hotkey time; UI hides before injection; foreground asserted
- [x] Swap setting inverts copy/paste chords end to end
- [x] Elevated-target refusal is reported (Diagnostics), never silent

## Comments

Implemented 2026-09-09 via TDD (red-green per slice) + two-axis code-review.
237/237 xUnit green (194 prior + 43 new), 0 errors, 0 warnings (.NET 8.0.425).
Seams: `CopyBackPlanner.Plan` (pure item→contents), `PngToDib.FromPng`
(pure PNG→CF_DIB, reverse of `DibToPng`), `DropFilesBuilder` (pure
DROPFILES + forced-copy effect), `CopyPasteChords.Resolve` (pure
shift×swap matrix), `PasteOrchestrator.ExecuteAsync` (over fakes for
writer/injector/foreground/elevation/images/delay + real SQLite `:memory:`
and real `CaptureService`) — store is real, never mocked. Win32 P/Invoke
(`Win32ClipboardWriter`, `Win32PasteInjector`, `Win32ForegroundWindow`,
`Win32ElevationProbe`) compiles with no warnings; real-clipboard behavior
is manual-smoke per Testing Decisions.
Review fixes folded in: elevation errors bias to suspect (a visible but
unopenable process reports elevated rather than failing silent UIPI),
PNG chunk-length guard against overflow, temp image dir cleanup in tests,
shared order-log proving write→hide→inject. Deferred by ticket boundary:
hotkey-time target capture source (ticket 15 passes the HWND in);
CF_DIB conversion of exotic PNG flavors (writer falls back to PNG format);
`PasteDelayMs` measurement on real targets (default 200, tunable).

Formal /code-review 2026-09-10 (diff 1cee807..5a583d5): Standards 0 hard, 0 actionable (all six smells judged leave-as-is); Spec 7 findings. Fixed: foreground asserted before elevation probe (stale handle → CopiedOnlyForegroundLost, never misdiagnosed as elevated). Open: HWND producer (15 shell), measured delay (manual smoke), Diagnostics-page wiring (shell), exotic PNG (writer PNG fallback). Suite 689/689. Status → resolved.
