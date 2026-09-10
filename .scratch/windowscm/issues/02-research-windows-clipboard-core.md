# Núcleo clipboard Windows

Type: research
Status: resolved

## Question

Definir o núcleo Win32 para WPF .NET 8: como monitorar e ler/escrever o clipboard com paridade (texto, imagem, arquivos), sem polling ingênuo.

Responder com APIs + formatos + edge cases:
- `AddClipboardFormatListener(HWND)` → `WM_CLIPBOARDUPDATE` em janela WPF (HWND oculto), `GetClipboardSequenceNumber` como fallback
- Formatos: `CF_UNICODETEXT/CF_TEXT`, `CF_BITMAP/CF_DIB`, `CF_HDROP` + `DragQueryFile` (copy vs cut, 1 vs N arquivos), `CF_HTML` se necessário
- Escrita de volta (copy/paste) + `SendInput` para colar no app focado (timing 250ms do original?)
- Single-instance (`Mutex` nomeado) + autostart chave `Run` + args `--hidden`
- O que o plugin Tauri/Electron NÃO cobre (por que WPF direto foi escolhido)

## Answer

Achados em `.scratch/windowscm/research/02-clipboard-core.md` (só fontes primárias MS Learn/.NET + docs Tauri/Electron; 8 seções + amostra C# P/Invoke + lacunas).

Load-bearing para a spec:
- Listener: `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` via `HwndSource.AddHook` (delegate com referência viva) ou message-only window (`HWND_MESSAGE`). `GetClipboardSequenceNumber` NÃO é notificação (doc proíbe polling) — só checagem pontual em ativação/foco.
- `CF_HDROP` = único formato Shell pré-definido; `DragQueryFileW(0xFFFFFFFF)` dá count (1→File, N→Files); cut vs copy via `"Preferred DropEffect"` (MOVE=cut). Bitmap: preferir `CF_DIB/V5`, persistir PNG via `PngBitmapEncoder`. `EnumClipboardFormats` com clipboard aberto (real antes dos sintetizados). `CF_HTML`: header com offsets em bytes + UTF-8 — v1 armazena opaco e reescreve verbatim.
- Ownership: copiar handle imediatamente, nunca liberar/lockar; `SetClipboardData` transfere ownership (`GMEM_MOVEABLE`); `OpenClipboard(NULL)`+`EmptyClipboard` zera o dono. STA thread, retry com backoff.
- Colar: `SendInput` serial + UIPI (falha silenciosa vs apps elevados); fluxo = capturar alvo no hotkey → esconder UI → assert `GetForegroundWindow` → injetar `Ctrl+V`. "250ms" do original vira constante tunável (sem base na doc).
- Single-instance: `Mutex` `Local\Nome+SID` + `createdNew` (ACL só existe em .NET Framework!) + handoff named pipe; autostart unpackaged = chave `HKCU…\Run` (`StartupTask` exige MSIX); arg `--hidden`.
- Gap confirmado: Tauri (texto/imagem/html/clear) e Electron (formatos crus sem parsing) não cobrem listener + `CF_HDROP` cut/copy + enum priorizado — justifica WPF+P/Invoke.
- Lacunas (spikes p/ fase `to-tickets`/implementação, não bloqueiam a spec): heurística de terminal (`Shift+Insert` vs `Ctrl+V`, detecção por classe/processo), delay pós-foco + retry policy por medição, `CFSTR_FILEDESCRIPTOR` (arquivos virtuais) fora do v1.
- Desbloqueia: `08` (com `03`); alimenta `04` (formatos→schema) e `05` (colar/incognito).
