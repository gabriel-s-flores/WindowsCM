# Stability and performance verification (real app on Windows)

Status: resolved
Type: task
Blocked by: large-window-placement-modes (stacked branch)

## Question

"Did you run the performance and stability tests with the new features? This
app needs to be rock solid."

Honest answer at the time: no. There were only functional tests (unit + CI).
This delivery creates the verification and fixes what it found.

## What now exists

- **Real-app smoke on the Windows CI** (`scripts/smoke/app-smoke.ps1`, runs on
  every PR): 300 copies (text, code, links, 2 MB text, 1600×900 images, file
  lists, emoji) with bursts; a baseline without WindowsCM; popup latency
  (under load and idle) and hotkey latency; memory and managed heap after a
  forced GC (`dotnet-gcdump`); placement modes; end-to-end auto-paste into
  Notepad via the real hotkey; error log.
- **Load/scale/fuzz tests in Core** (isolated collection): production
  composition with 5 threads; 2,100 items with 20 × 1 MB; 1,500 corrupted
  `settings.json` files; 20,000 random monitor layouts.

## Bugs found and fixed

| # | Severity | Origin | Bug |
|---|---|---|---|
| 1 | Critical | main | WindowsCM blocked other apps from copying: 131 of 300 copies failed (0 of 60 without it). It read the clipboard on the change notification itself, contending with the source. It now waits for 100 ms of quiet (bursts read once, 500 ms cap). |
| 2 | High | main | Screenshot encoded to PNG with the clipboard open (~0.5 s at 4K): nobody could copy/paste during that interval. It now only copies bytes and encodes after closing. |
| 3 | High | main | Print Screen / `SetImage` images were never captured (`BI_BITFIELDS` DIB rejected). |
| 4 | High | main | A `settings.json` with a null category list kept the app from opening. |
| 5 | High | PR #6 | Pasting after opening from the tray failed: Windows gave the focus back to the taskbar. It now restores the target before hiding the popup. |
| 6 | Medium | main | Pasting into a hung window could hang WindowsCM (`AttachThreadInput`). |
| 7 | Medium | main | A corrupted `settings.json` was overwritten without a copy (now `.corrupt`). |
| 8 | Medium | main | The reader requested every format (forces Excel to render HTML for a copy that becomes an image). |
| 9 | Medium | — | Memory: the GC retained ~100 MB more; `GCConserveMemory=7`. |

False alarms investigated (bugs in the test, not in the app): links
"disappearing" (`"$i?ref"` in PowerShell), free mode "out of place" (1024 px
screen), paste "failing" (fixed wait on a shared runner).

## Numbers (Windows runner, 2 vCPU, no GPU)

- Other apps' clipboard: 0 failures in 300 copies.
- Popup: ~150 ms idle (p50), 66–190 ms under load (p50).
- Hotkey → popup: 246–339 ms warm; 630–780 ms on the 1st open (JIT).
- Managed heap after GC: stable ~60 MB (no leak, 600 copies).
- Private memory: settles at ~190–210 MB; working set ~300 MB.

## Not covered (honest limits)

- Real multi-monitor hardware and high DPI (runner: 1 monitor, 96 DPI).
- Real sources with delayed rendering (Office/Excel/browsers).
- Days-long sessions; a hung target app (covered only by code/review).

## Suggested next step

- `PublishReadyToRun` in `build-dist.ps1` to reduce the 1st open (measure
  with the smoke against the published executable).
