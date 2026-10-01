# WindowsCM stability and performance audit

Date: 2026-09-30 (local time)

Environment: Windows 11 Education, build 26200; AMD Ryzen 7 3800X;
.NET 8.0.31; Release builds. Timings are observations on this desktop,
not cross-machine guarantees or comparisons with other clipboard managers.

## Confirmed issues and fixes

| Issue | Reproduction before the fix | Change | Verification |
| --- | --- | --- | --- |
| Unavailable image paths blocked the UI | Production image converter against an unavailable UNC share stopped the UI heartbeat for over five seconds. | Resolve paths without disk access; probe file metadata and decode thumbnails only on workers. Missing/failed results are cached; prewarming shares the same workers. | 20 converter calls: image 5.02 ms, link 6.49 ms; unavailable-share watchdog passed. |
| WPF item-key hashing scanned full clipboard payloads | A managed stack captured the UI inside `ClipboardItem.GetHashCode` from `VirtualizingStackPanel.GetContainerSizeForItem`. 1,000 hashes of an 8M-character content/metadata payload timed out after five seconds. | Hash only scalar item fields in constant time. Record value equality still compares all fields. | 1,000 hashes: 0.21 ms in the final suite; equality regression passed. |
| Extreme image aspect ratios expanded thumbnails | A 1,000 x 10 image decoded to 18,000 x 180 pixels. Width-only banners similarly expanded tall images. | Read source dimensions and fit into 320 x 180 pixels without upscaling; decode from a shared stream without a full encoded-file byte-array copy. | Wide, tall, one-pixel and 4K PNGs passed in both thumbnail modes; all bitmaps frozen, sources deletable after decoding. |
| Malformed HTML exhausted preview workers | 20,000 unfinished `<link rel='image_src'` tags timed out after three seconds, within the existing 1 MB HTML response limit. | Use nonbacktracking matching for the four metadata regexes. | Adversarial HTML: 2.92–4.61 ms in the final suite; normal Open Graph, Twitter and title precedence tests passed. |

The earlier inline-`#` tokenizer freeze remains covered by regression tests
and the expanded character/tokenizer fuzz corpus.

## Stress results

- **Full Core suite:** 1,373 passed, zero failures, 26 seconds.
- **Seeded content fuzz:** 20,000 tokenizer inputs; 5,000 classifier inputs;
  40,000 item-format combinations; 60 almost-valid color strings; 60,000
  unfinished HTML tags across three cases. Includes NUL, unpaired surrogates,
  Unicode, unfinished comments/strings, invalid JSON and mixed item kinds.
- **60-second concurrent pipeline soak:** 9,664 captures, 12,676 UI/model
  operations, 257,566 preview operations, 2,142 IPC-dispatch operations and
  1,704 incognito operations. SQLite integrity check passed; unpinned history
  stayed within its limit. Native clipboard/paste boundaries are fakes in
  this test; the store and Core pipeline are production implementations.
- **History scale:** 2,100 persisted items, including twenty 1 MB texts.
  Latest-item lookup 0.016 ms, lookup by ID 0.040 ms, capture/eviction 7.077 ms,
  popup-model refresh 33.2 ms and search 33.7 ms (averages).
- **Production WPF rendering:** 2,000 mixed cards, 16,000 forward/backward
  scroll/layout steps across large/compact and horizontal/vertical layouts.
  Includes shared 2 MB text fixtures, code, emoji, colors, 4K images, corrupt
  and missing images, files, links and malformed metadata. Refresh, wheel,
  theme, language and hide/show gestures are interleaved.
- **Normal workflow:** 350 popup cycles over 1,000 distinct SQLite items,
  search debounce flushed by navigation, activation identity, pins, 25
  deletions, empty searches, reversed ordering, resource replacement,
  high contrast and fourteen settings-window lifecycles. All passed.

| UI measurement | Median | p95 | Maximum |
| --- | ---: | ---: | ---: |
| Scroll plus synchronous layout, 2,000 cards | 0.11 ms | 3.50 ms | 180.33 ms |
| Show plus layout in the card stress | — | 36.34 ms | — |
| Full show-at-cursor in the workflow | 44.28 ms | 64.73 ms | 321.86 ms |

Card stress peak private memory was 322.65 MB and peak handle count was 699.
After 50 warmup cycles and 300 more workflow cycles, private memory decreased
by 2.45 MB, managed memory decreased by 0.045 MB and handles increased by one.
The workflow ended with 12.01 MB of live managed memory and only the two
intended reusable popup windows registered.

An initial lifecycle harness continuously scheduled `Background` callbacks,
starving WPF's `ContextIdle` cleanup of disconnected visual trees. Yielding
at `ApplicationIdle` between gestures removed that artificial retention.
This was corrected in the test harness, not reported as an application leak.

## Reproduction and artifacts

```powershell
dotnet test tests/WindowsCM.Core.Tests -c Release

$env:WINDOWSCM_SOAK_SECONDS = '60'
dotnet test tests/WindowsCM.Core.Tests -c Release --filter ProductionSoakTests
Remove-Item Env:WINDOWSCM_SOAK_SECONDS

powershell -ExecutionPolicy Bypass -File scripts/smoke/ui-stress.ps1 -Cards 2000

powershell -ExecutionPolicy Bypass -File scripts/build-dist.ps1
```

Detailed local reports are under `TestResults/stress/` (ignored build
artifacts). The reusable harness and its isolation/threshold documentation
are in [scripts/smoke/WindowsCM.UiStress](../../scripts/smoke/WindowsCM.UiStress/README.md).

## Coverage limits

These UI timings measure synchronous WPF layout, not GPU presentation
latency or refresh-rate consistency. Cold loading and JIT contribute to the
maximum values. The run does not establish that WindowsCM is faster than
other Windows applications or that every possible crash has been excluded.

The installed application's data and settings were not reset. The native
clipboard, Notepad auto-paste, installer startup and physical monitor-change
smokes were not run on this user profile: the existing `app-smoke.ps1` and
`scroll-smoke.ps1` deliberately delete user data and belong on a disposable
Windows profile. The isolated harness uses the same production WPF windows
and converters without starting those external integrations.

Portable and installer packages are regenerated under `dist/`; rebuilding
does not replace the currently installed executable.
