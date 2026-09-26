# Copyous parity inventory

Type: research
Status: resolved

## Question

Audit the Copyous code (https://github.com/boerdereinar/copyous, branch main) and produce the canonical full-parity inventory: every type, every pref, every shortcut, every default action, DB schema, tags, themes — with file:line evidence.

Cover at least:
- `src/lib/common/constants.ts` (ItemType, Tags + hex, HljsLanguages)
- `src/lib/misc/clipboard.ts` (Image>File>Text pipeline, Link/Character/Color/Code/Text, MD5 dedup, exclusions, incognito)
- `src/lib/database/gda.ts` + `resources/database/database.sql` (schema v2, clear/protect/deleteOldest)
- `src/lib/common/actions.ts` + prefs actions (model, matching, defaults, Default Actions per type)
- `src/prefs.ts` + `resources/schemas/*.gschema.xml` (full General/Customization/Shortcuts/Actions list)
- `src/lib/common/dbus.ts` (Toggle/Show/Hide/ClearHistory)
- `src/lib/ui/clipboardDialog.ts` + items/* + themes SCSS (layout, cards, dark/light/high-contrast colors)
- Dependencies (GSound, Soup link preview, highlight.js, GdkPixbuf, VirtualInputDevice paste)

## Answer

Findings in `.scratch/windowscm/research/01-parity-inventory.md` (shallow clone at `C:\Users\gabri\AppData\Local\Temp\opencode\copyous`, commit `fa103e3`; code+schemas+SQL+README only, every claim with `copyous:file:line`). 8 sections, 440 lines, ~28k chars, all areas of the Question covered.

Load-bearing for the WPF spec:
- Portable data: 8 types (Text, Code, Image, File, Files, Link, Character, Color), 9 fixed hex tags, 1 SQLite v2 table `UNIQUE(type,content)` + `pinned/tag/datetime/metadata/title`.
- Classification is a pure rule (http prefix + `GLib.uri_is_valid` → Link; `Intl.Segmenter` grapheme ≤ max-chars → Character; `Color.parse` 10 CSS Color 4 color spaces → Color; `highlightAuto` slice 10k, `relevance/n>=3` → Code; otherwise Text). Pipeline Image > File > Text, MD5 dedup, `prevClipboard` set before `shouldSave` (incognito does not leak).
- Actions (`command/color/qrcode`, regex+types, stdin/stdout, timeout 30s, per-type defaults) become Command + JSON almost 1:1. DBus becomes named pipe/CLI. Prefs (~70 keys, 4 pages) become Settings + UI.
- Hard: global Win32 monitor (CF_* formats, owners that disappear; exclusions by HWND/process, not WM_CLASS), thumbnails (WIC/Shell providers + MediaFoundation), UI (modal H/V dialog, cards, search with Collator, 3 SCSS themes — highest cost), GSound (bundle our own wavs), full color parser + glob→regex need a faithful port.
- Gaps: `actionsGroup/actionSubMenuDialog/editDialog/qrCodeDialog/scrollContainer/layout/theme/compatibility/icons/utils/shortcutRow` not audited line by line; direction of `select_order_by(datetime)` in Gda assumed DESC; limits (image size, QR capacity, notification rate-limit) not found; divergences noted (8 shortcut groups in the UI vs 7 in the ticket; `CustomColorScheme.HighContrast` without a gschema; SpinRows 300×200 vs schema 250×170, harmless).
- Unblocks: `04` (persistence), `05` (actions), `06` (settings grilling) — can be claimed now.
