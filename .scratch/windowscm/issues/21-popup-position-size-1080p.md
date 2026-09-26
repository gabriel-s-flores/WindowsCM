# 21: Popup abre sempre no cursor com tamanho estável em 1080p

**What to build:** o popup passa a abrir de forma determinística em tela 1920x1080 a 100%: mesma posição relativa ao cursor e mesma largura a cada abertura com o mesmo histórico, sem saltos nem redimensionamentos visíveis ao digitar a busca.

**Blocked by:** 20 (instrumentação temporária + baseline + esqueleto do smoke).

**Status:** resolved

- [x] Abertura no centro e nas bordas direita/inferior ancora no cursor com deslocamento fixo e clamp dentro da área de trabalho
- [x] Largura fixa e altura estável para o mesmo conteúdo; filtrar a busca não causa flicker de tamanho
- [x] Primeira abertura após iniciar o app posiciona igual às seguintes
- [x] Teste de posicionamento cobre os cantos 1080p e o smoke valida Left/Top pelo log + screenshot

## Comments

Implemented 2026-09-11. Full suite **710/710 green** (699 prior + 11 new),
filtered popup/paste/tray **146/146 green** (135 + 6 corners + 5 sizing),
`dotnet build` 0 warnings/errors. Live smoke 4 PASS / 0 FAIL / 3 SKIP
(SKIPs are tickets 22-24 placeholders).

Root causes found live (first smoke run failed 0/4 and proved each one):
- First open measured `0x0`: a never-shown WPF Window lays out to zero, so
  `Measure` before `Show` sized/clamped the first open differently.
  Fix: `ShowAtCursor` now does `Show()` + `UpdateLayout()` first, then
  places from `ActualWidth/ActualHeight` synchronously before first paint
  (transparent until placed, so no flash at a stale position).
- Placement used `DesiredSize.Width` (content-dependent) for the
  right-edge clamp while the window renders fixed 380. Fix: new pure
  `PopupSizing` seam (`FixedWidth=380`/`MaxHeight=520` XAML parity +
  `ClampHeight`) enforced by the shell every open.
- `RefreshView` ran after measuring, so the first open measured an empty
  list. Fix: populate before placing.
- `PresentationSource` is null before first `Show` (Identity transform).
  Fix: `EnsureHandle` + `HwndSource` transform (identical at 100% DPI,
  correct above it).
- Search flicker: `SizeToContent=Height` resized the window on every
  keystroke. Fix: frozen to `Manual` with the laid-out size pinned after
  each open; `OnSearchChanged` only swaps rows (list scrolls internally).
- Locale bug: `:F1` log formatting used pt-BR decimal commas
  (`cursorDip=904,0`), breaking deterministic parsing. Fix:
  `FormattableString.Invariant` on the `popup-open` line.

Files:
- `src/WindowsCM.Core/Popup/PopupPlacement.cs` (+`PopupSizing` seam).
- `src/WindowsCM.App/PopupWindow.xaml.cs` (order + freeze + invariant log).
- `tests/.../Popup/PopupSizingTests.cs` (new, 5 tests) +
  `PopupPlacementTests.cs` (+6: center/TL/TR/BL/BR at 1920x1032 + determinism).
- `smoke-ui.ps1`: `popup-1080p` automated — parks cursor at center (x2),
  right edge, bottom-right corner via `--show`/`--hide` pipe handoff,
  validates `final=` vs cursor+12 clamp and `size=` width=380 from the
  `WCM20:popup-open` log (3x retry with region/gate checks so a moved
  mouse retries instead of validating the wrong corner), compares
  first-vs-second open, screenshots to `%TEMP%\WindowsCM-popup-*.png`.

Live proof (1920x1080 @100%, work area 1920x1032, 7 items):
`center cursorPx=960,540 final=972,552 size=380x465` first AND second open
identical; `right (1880,500) final=1540,512`; `corner (1880,1000)
final=1540,567`. Screenshots in report.

Left uncommitted in the tree (prior tickets 19/20 are also staged/untracked,
so a per-ticket commit would sweep others' work): this ticket's delta is
`PopupPlacement.cs`, `PopupWindow.xaml.cs`, `PopupSizingTests.cs`,
`PopupPlacementTests.cs`, `smoke-ui.ps1`, `smoke-report.md` + this file.
