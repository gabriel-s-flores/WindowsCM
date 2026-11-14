# Isolated WPF stress harness

Run on a Windows desktop with .NET 8 installed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/smoke/ui-stress.ps1 -Cards 2000
```

The harness runs the production large popup, compact popup, settings window,
converters and thumbnail decoder. It constructs `App` without calling its
startup, so it never starts clipboard capture, the tray, global hotkeys, IPC,
or paste injection. Fixtures use an in-memory database or a uniquely named
temporary directory. Settings windows save into that directory, and the app's
log notes (popup open timings) go to a temporary file. Favicons are
seeded in memory to prevent downloads and user-cache writes.

Scenarios run in separate processes. Each has a watchdog that fails after
five seconds without a UI heartbeat. The runner stops at the first failure
and writes reports under `TestResults/stress/ui/`.

- `remote-image` / `remote-link`: repeated production converter calls against
  a TEST-NET address representing an unavailable image share. The UI lookup
  must complete within 250 ms; filesystem access belongs to workers.
- `image-shapes`: landscape, portrait, one-pixel and 4K PNGs, both thumbnail
  modes. Check dimensions, freezing, no upscaling, and unlocked source files.
- `cards <count>`: forward/backward scrolling through both popup orientations,
  refresh bursts, normal/precision wheel events, resource and theme changes,
  and hide/show cycles. Includes multi-megabyte text, Unicode, code, missing
  and corrupted images, links, files, emoji and colors. Count: 100–10,000.
- `workflow`: 350 cycles over 1,000 real SQLite items: show-at-cursor, debounced
  search flushed by navigation, selection/activation identity, pins, deletion,
  empty results, reversed ordering, languages, themes and settings windows.
  After 50 warmup cycles, memory/handle growth budgets are 64 MB / 100 handles.

- `perf card-updates`: image cards realized before their thumbnail is
  decoded must show it once the decode lands, in all four layouts, without
  the popup being reopened. Run by `ui-stress.ps1`.

Timing scenarios (numbers are printed, not asserted; the popups appear on
screen; 60 synthetic items in memory, favicons seeded in memory):

```powershell
$exe = 'scripts/smoke/WindowsCM.UiStress/bin/Release/net8.0-windows/WindowsCM.UiStress.exe'
& $exe perf firstshow-compact no-large       # first ShowAtCursor, cold
& $exe perf firstshow-compact no-large warm  # same, after the startup warm-up
& $exe perf firstshow-large
& $exe perf wheel    # distance moved per wheel delta, all four layouts
& $exe perf frames   # offset changes, layout passes, UI CPU, frame pacing, card refresh cost
```

`perf frames` counts distinct `RenderingTime` values as frames (WPF raises
`Rendering` several times per frame). A loop that writes the offset on every
callback keeps WPF rendering continuously and inflates that count; compare
it together with the UI thread CPU.

Scroll timings measure synchronous WPF layout, not GPU presentation latency
or monitor frame rate. Initial loading/JIT is included in maximum timings.
The lifecycle scenario yields at `ApplicationIdle` between gestures so WPF
can clean disconnected visual trees; an uninterrupted `Background` loop
starves its `ContextIdle` cleanup and produces artificial retained memory.

For a longer concurrent Core pipeline soak (capture, query, pin/delete,
previews, IPC dispatch and incognito), use:

```powershell
$env:WINDOWSCM_SOAK_SECONDS = '60' # 3–300; defaults to 3
dotnet test tests/WindowsCM.Core.Tests -c Release --filter ProductionSoakTests
Remove-Item Env:WINDOWSCM_SOAK_SECONDS
```

Native clipboard interception, cross-process auto-paste and installed startup
remain covered by `app-smoke.ps1` and `scroll-smoke.ps1`. Those scripts reset
the current user's data and must only run on a disposable Windows profile.
