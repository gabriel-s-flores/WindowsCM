# 10: Classifier and content types

**What to build:** raw clipboard content becomes correctly typed items: the
probe order, the text sub-pipeline with its thresholds, file counting with
copy/cut parsing, and rejection rules — all as pure logic with direct unit
tests and zero mocks.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Probe order Image > File > Text; richest representation wins
- [x] Link on http-prefix plus valid URI; character on grapheme count within max-characters
- [x] Color via ported grammar (named/hex/rgb/hsl and friends); code via auto-detect slice with relevance/density threshold; plain-text fallback never loses content
- [x] Empty/whitespace-only input discarded; password-manager hint rejected
- [x] Single path becomes File, many become Files, operation parsed (default copy)
- [x] Image extension follows source mimetype; content hash (MD5) reported for dedup

## Comments

Implemented 2026-09-09 via TDD (red-green per seam) + two-axis code-review.
135/135 classification tests green, 167/167 full suite, 0 errors, 0 warnings
(.NET 8.0.425). Seams: `Classifier.Probe/ClassifyText/ClassifyFiles/
ClassifyImage`, `ColorParser`, `CodeDetector`, `FileDrop`, `ImageContent`,
`ClipboardHash`, `GraphemeCounter`, `LinkDetector`, `SensitiveHints` —
all pure, zero mocks. Conversions deferred to 13 per agreement.
Review fixes folded in: `Relevance` privatized, grapheme comment corrected
to ICU parity (flag/ZWJ verified = 1 element). Declined: C1/C2/C3 rename
(Copyous parity, consumed by 13), ParseFunctional table refactor (one line
per grammar production), extension-split/`http`-prefix case-sensitivity
(exact parity), zero-alpha maps to 0 (documented correctness deviation).
Deferred by ticket boundary: code language ids (highlight spike, 14),
file:// URI assembly + write-if-absent and prevClipboard/incognito gate
(stateful capture loop, 11).

Formal /code-review 2026-09-10 (diff 1b1e905..e6cc278): Standards 0 hard + 4 smells (kept: dead Relevance, ColorParser table, C1/C2/C3, copy/cut pair — small, stable); Spec 8 findings. Fixed: file hash/content agreement — stored content is now canonical local paths, FileHash hashes the canonical form, equivalent URI spellings store identically, SuppressionHash hashes stored content verbatim (no double-unescape). Text hashing unchanged (verbatim = stored; trimming would break agreement). Open: image extension mapping (jpeg/svg+xml — needs Copyous file-evidence), Windows opt-out + GNOME payload extras (kept), extension-split/case items (exact parity, kept). Suite 689/689. Status → resolved.
