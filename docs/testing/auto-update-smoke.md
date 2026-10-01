# Auto-update and README validation

Validated on Windows on 2026-09-30.

## Automated tests

`dotnet test WindowsCM.sln -c Release` passed: 1,390 tests, zero failures or skips.

Update coverage includes stable version selection, installed versus portable assets, incomplete releases, invalid versions, foreign download URLs, checksums, corrupt payload cleanup and the absence of releases. Windows integration tests execute the production portable replacement script in an isolated fixture directory: successful replacement, failure preserving the original executable, and literal paths containing spaces, apostrophes and dollar signs. Localization tests enumerate every string property in both catalogs.

## WPF smoke tests

`scripts/smoke/ui-stress.ps1 -Cards 500` passed all five scenarios:

- Unavailable image and link image paths return without blocking the UI.
- Extreme image aspect ratios remain bounded and release source files.
- 500 items, 4,000 scroll/layout steps across large and compact horizontal and vertical layouts. Maximum step: 190 ms; opening p95: 61 ms.
- 350 workflow cycles exercise search, selection, pin/delete, empty results, reversed ordering, language/theme changes, settings and popup lifecycle. Opening p95: 77 ms; maximum: 399 ms. Private memory and handles did not grow after warm-up.

`dotnet run --project scripts/smoke/WindowsCM.UiStress -c Release -- screenshots assets` passed and generated seven English PNG captures of production visual trees. It checks pinned search, isolated incognito cleanup with normal history preservation, settings navigation and live language switching in an open update window. The update screenshot uses fixture version 1.2.3.

## Distribution

`scripts/build-dist.ps1` produces the self-contained portable executable, ZIP and Inno Setup installer in `dist/`.

## Validation boundary

No GitHub release was published during this task. A full installed-app upgrade through a newly published release has not been exercised. Existing destructive desktop smoke scripts were not run against the user's profile; the isolated harness preserves personal history and settings.
