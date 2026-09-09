# 14: Link previews, code highlight and sounds

**What to build:** items read rich and copies confirm audibly: link metadata
fetched and cached, code highlighted with plain-text fallback, the nine
sounds with volume mapping, balloon feedback and icon flash.

**Blocked by:** 10 (classifier and content types).

**Status:** resolved

- [x] HTML-only link metadata (title/description/image) cached by URL hash; offline yields no preview; regex exclusions honored
- [x] Code renders highlighted, degrading to plain text when detection/highlighter is unavailable
- [x] All nine sounds play with exponential dB-to-gain mapping; default is silent
- [x] Copy feedback shows a balloon (per setting) plus the icon flash; no OS toast dependency

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis self-review
(Standards + Spec).
392/392 xUnit green (338 prior + 54 new), 0 errors, 0 warnings (.NET 8.0.425,
no new packages — BCL only).
Seams: `LinkMetadataParser` (og:title|twitter:title|<title> precedence,
reversed meta attrs, entity decoding, relative resolution),
`LinkExclusions` (2s timeout, bad/timeout = not excluded, fail open),
`LinkImageCache`/`ILinkImageCache` (MD5(url) file, save-if-absent + sweep),
`LinkPreviewService` over `ILinkPreviewHttp`/`ILinkImageCache` fakes
(HTML-only, image/* direct, offline => null, cache-hit skips download,
failed og:image still returns text), `LinkPreviewHttpClient` (singleton,
5s timeout, product UA, header-first reads), `ItemMetadataJson`
(link/code/html merge, corrupt => null), `CodeLanguageMap` (curated hljs
subset) + `CodeHighlightPlanner` (highlighted only when known + available
+ enabled, else plain text), `SoundName` (None + 8 wavs = 9 options,
silent default) + `SoundGain` (10^(dB/20) clamped 0..1) + `SoundAssets`
+ `SoundFeedback` over `ISoundPlayer` fake, `CopyFeedbackService` over
`ICopyNotifier`/`IIconFlasher` fakes (balloon per send-notification,
3x65ms flash per wiggle-indicator, zero toast references).
MediaPlayer playback, NotifyIcon balloon/flash adapters and the AvalonEdit
control live in the UI layer (WPF PresentationCore, not net8.0 Core);
Core owns policy + mapping. Full 192-row hljs table deferred post-v1
(map misses are plain text by design). Language-id emission into history
still awaits a detector spike producing ids (`ItemMetadataJson.Merge`
is the merge point, per the CaptureService comment).
Review fixes folded in: redundant `with` copy dropped, merge comment
corrected to top-level keys, case-insensitive merge dict, NaN-gain
comment, per-language UI guard note, entry->pattern wording (CONTEXT.md).
