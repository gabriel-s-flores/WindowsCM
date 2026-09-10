# Hotkeys globais + tray + popup

Type: research
Status: resolved

## Question

Definir hotkeys + tray + posicionamento do popup no Windows com WPF.

Responder:
- `RegisterHotKey` / `UnregisterHotKey` → `WM_HOTKEY`: IDs, modificadores, falha se ocupada, por que `Win+V`/`Win+Shift+V` fora, defaults `Ctrl+Shift+V` (abrir) + `Ctrl+Shift+Alt+V` (incognito) configuráveis
- Tray: `NotifyIcon`/`H.NotifyIcon` (WPF), tooltip, clique abre, menu `Abrir/Incognito/Limpar(manter pins+tags)/Configs/Sair`, ícone + comportamento single-click vs double
- Popup: janela `ShowActivated=false`, posicionar em `GetCursorPos` (mouse) vs caret do texto (caret API), multi-monitor/DPI, foco/perda de foco (auto-hide?), `Esc` fecha?
- Mapear atalhos internos do Copyous (Enter/Space copy/paste, Ctrl+Enter ação default, Ctrl+S pin, Delete, Ctrl+0..9 jump, Alt pinned, Ctrl+Tab tipo, Ctrl+` tag) para WPF

## Answer

Achados em `.scratch/windowscm/research/03-hotkeys-tray-popup.md` (só fontes primárias + repos oficiais; 6 seções + amostras C#/XAML + lacunas).

Load-bearing para a spec:
- `RegisterHotKey(hWnd,id,mods,vk)` por HWND, ids app `0x0000–0xBFFF`, `WM_HOTKEY=0x0312`; re-registrar exige `UnregisterHotKey` explícito. `MOD_WIN` reservado + `Win+V` do SO → fora (confirma Q4). `MOD_NOREPEAT` sempre (Win7+). Falha = retorno zero + `GetLastError` (`SetLastError=true`); hooks via `HwndSource.AddHook`.
- Defaults: `Ctrl+Shift+V` colide (colar-sem-formatação Windows/VS Code) mas segue configurável; `Ctrl+Shift+Alt+V` risco baixo. Persistir em `Properties.Settings` user-scoped + `Save()`; troca runtime = Unregister+Register.
- Tray: **`H.NotifyIcon.WPF` (`TaskbarIcon`, MIT)** sobre WinForms `NotifyIcon` (comandos, binding). Esquerdo = toggle popup, direito = menu `Abrir/Incognito/Limpar(manter pins+tags)/Configs/Sair`; double-click só alias.
- Popup: `ShowActivated=False` + `Topmost`, `Deactivated→Hide()`, `Esc` fecha, animação ~150ms via Storyboard. **Posição v1 = `GetCursorPos` → DIPs (`TransformFromDevice`/`GetDpi`) + clamp em `WorkingArea`**; caret global (`GetGUIThreadInfo`+`ClientToScreen`, UIA `GetCaretRange`) fica p/ v2. Per-monitor DPI via manifesto.
- **Desvio de paridade: `Alt` sozinho vira `Key.System`/modo menu no WPF → toggle-pinned vira `Alt+P`** (contradiz o Copyous, reaberto pelo SO; registrar na spec). `Enter/Space/Delete/Ctrl+A` em `PreviewKeyDown` escopado (fora da busca), resto em `Window.InputBindings`.
- Lacunas (implementação): `ERROR_HOTKEY_ALREADY_REGISTERED` (hip. 1409, confirmar `winerror.h`), versão/TFMs `H.NotifyIcon.Wpf` no NuGet, `GetCaretRange` UIA, gestos exatos `ApplicationCommands`, limite tooltip `szTip`/balloon Win11.
- Desbloqueia: `07` (com `01`) e `08` (com `02`).
