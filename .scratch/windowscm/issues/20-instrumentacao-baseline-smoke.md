# 20: Instrumentação temporária + baseline de testes + esqueleto do smoke

**What to build:** a base observável para o fix: logs temporários que provam onde o popup abre, qual alvo de colagem foi capturado e qual foi o desfecho da ativação, mais baseline verde e esqueleto do smoke automatizado prontos para os tickets seguintes usarem.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Log temporário com prefixo único cobre: abertura do popup (cursor, área de trabalho, tamanho medido, posição final, nº de items visíveis), ativação (item escolhido, modificador, alvo capturado vs atual pós-hide, desfecho + diagnóstico) e tray (visível, resultado do single-instance)
- [x] Baseline `dotnet test` filtrando popup/paste/tray passa e o nº de testes é registrado no ticket
- [x] Esqueleto `smoke-ui.ps1` builda, roda os testes, inicia o app, coleta o log temporário e emite `smoke-report.md` com PASS/FAIL por passo
- [x] Nenhum log temporário vaza para o comportamento do usuário além de arquivo em TEMP

## Comments

Implemented 2026-09-11. Baseline: filtered popup/paste/tray **135/135 green**;
full suite **699/699 green** (689 prior + 10 new), `dotnet build` 0 warnings/errors.

- `src/WindowsCM.Core/Diagnostics/TempSmokeLog.cs` (new, TEMP ticket 20 — removed in 25):
  static file-only logger, unique prefix `WCM20`, single file
  `%TEMP%\WindowsCM-20-smoke.log` (`WINDOWS_CM_SMOKE_LOG` override for tests),
  thread-safe, never throws, `Preview()` truncates content to one line.
- `PopupWindow.PlaceAtCursor` logs `popup-open` (cursor px/DIP, work area, measured
  + clamped size, final Left/Top, visible count, incognito).
- `App.OnHotkey` logs `activate/capture` (slot + captured target HWND);
  `App.ActivateAsync` logs `activate/result` (item id/kind/preview, shiftHeld,
  runDefault, captured vs current-after-hide HWND, outcome chord, diagnostics)
  for paste, default-action and missing-item paths.
- `TrayManager` ctor logs `tray` (visible + tooltip); `App.OnStartup` logs
  `single-instance` (Decide outcome + startHidden). File-only: no UI/setting/history touch.
- `tests/WindowsCM.Core.Tests/Diagnostics/TempSmokeLogTests.cs` (new, 8 tests):
  prefix, TEMP default path, write format, clear, preview escape/truncate.
- `smoke-ui.ps1` (repo root, TEMP until 25): build → filtered tests → park stale
  instances → launch `--hidden` with 30s poll → collect log → `smoke-report.md`
  (PASS/FAIL table + log excerpt). Live run: 3 PASS / 0 FAIL / 4 SKIP
  (SKIPs are the manual placeholders tickets 21–24 automate).
- Drive-by fixes required for a green baseline: `App.ShowQr` was missing
  (PopupWindow called it — build break in uncommitted issue-19 work);
  `SetSelectedIndex_FollowsClick_Clamped` saved all items at the same minute so
  newest-first order was ambiguous — saves now use minute 0/1/2.
- Log excerpt observed live:
  `[WCM20:single-instance] outcome=IsPrimary startHidden=True`,
  `[WCM20:tray] visible=True tooltip=WindowsCM`.
  `popup-open`/`activate` lines exercise the same logger; interactive proof is
  tickets 21–24 via this skeleton.
