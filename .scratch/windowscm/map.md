## Destination

Spec buildável de um app Windows (WPF .NET 8) com paridade total do Copyous — tipos, pins, tags, ações, atalhos, configs, estilo + botão no system tray — pronta para `to-spec`.

## Notes

- Stack trancada: **C# WPF (.NET 8)**. Solo Windows-only, sem shim.
- Hotkeys default: **abrir `Ctrl+Shift+V`**, **incognito `Ctrl+Shift+Alt+V`**, configuráveis. `Win+V` / `Win+Shift+V` fora (reservados do SO).
- Tray: clique abre popup; menu = `Abrir / Incognito / Limpar (manter pins+tags) / Configs / Sair`.
- Distribuição: **portable single-file + instalador Inno Setup** (MSIX depois). Autostart via chave `Run`. Single-instance via `Mutex` nomeado. Alvo **Win10 20H2+ / Win11**.
- Mapa de equivalências Linux→Windows trancado: `Libgda→Microsoft.Data.Sqlite`, `GSound→System.Media`, `DBus→tray+CLI/named-pipe local`, `Soup→HttpClient`, `hljs→highlight local`, `paste virtual→SendInput`, `GSettings→JSON local`, `nautilus/xdg-open→ShellExecute`.
- Licença: **GPL-3.0-or-later** (igual ao original).
- Skills por ticket: `grilling` + `domain-modeling` em todo HITL; `research` via subagente em AFK; `prototype` para dúvida de UI/comportamento.
- Tracker: local markdown. Frontier = `issues/` abertas, desbloqueadas, unclaimed. `Status: claimed` antes de trabalhar; `Status: resolved` + `## Answer` ao resolver.
- Fonte original: https://github.com/boerdereinar/copyous (8 tipos: Text, Code, Image, File, Files, Link, Character, Color; DB v2; 9 tags; actions.json; 4 páginas prefs; DBus Toggle/Show/Hide/ClearHistory).

## Decisions so far

- [Stack WPF .NET 8](issues/01-research-copyous-parity-inventory.md): decisão de charting Q3 — sem shim para `AddClipboardFormatListener` + `CF_HDROP` + `NotifyIcon` + `Microsoft.Data.Sqlite`. (contexto: esta sessão de charting)
- [Hotkeys + tray defaults](issues/03-research-hotkeys-tray-popup.md): decisão de charting Q4 — `Ctrl+Shift+V` / `Ctrl+Shift+Alt+V` + menu tray de 5 itens. (contexto: esta sessão de charting)
- [Portable + Inno](issues/02-research-windows-clipboard-core.md): decisão de charting Q5 — Win10 20H2+/Win11, single-file + Inno, `Run` + `Mutex`. (contexto: esta sessão de charting)
- [Mapa de equivalências](issues/01-research-copyous-parity-inventory.md): decisão de charting Q6 — trancado acima. (contexto: esta sessão de charting)
- [GPL-3.0-or-later](issues/08-grilling-distribuicao-licenca.md): decisão de charting Q7 — manter. (contexto: esta sessão de charting)
- [Inventário de paridade Copyous](issues/01-research-copyous-parity-inventory.md): 8 tipos + 9 tags hex + SQLite v2 + pipeline Image>File>Text + classificação pura + actions.json + ~70 prefs + DBus; difícil = monitor Win32, thumbnails, UI, GSound, parser de cor. Detalhe em `research/01-parity-inventory.md` (commit fa103e3). Desbloqueia 04, 05, 06.
- [Núcleo clipboard Windows](issues/02-research-windows-clipboard-core.md): `AddClipboardFormatListener`+`WM_CLIPBOARDUPDATE` (HwndSource/message-only), sem polling; `CF_HDROP`+`Preferred DropEffect`, DIB→PNG, `CF_HTML` opaco v1; `SendInput`+UIPI com fluxo captura-alvo→assert→injeta; `Mutex Local\`+handoff pipe, `Run` autostart; gap Tauri/Electron confirmado. Detalhe em `research/02-clipboard-core.md`. Spikes (terminal, delay, FileDescriptor) adiados p/ `to-tickets`. Desbloqueia 08.
- [Hotkeys + tray + popup](issues/03-research-hotkeys-tray-popup.md): `RegisterHotKey` ids `0x0000–0xBFFF` + `NOREPEAT`, `Win+V` fora; **`H.NotifyIcon.WPF`** escolhido; esquerdo toggle/direito menu 5 itens; popup `ShowActivated=False`, `Deactivated→Hide()`, cursor-first v1 + clamp DPI, caret UIA v2; **desvio: toggle-pinned `Alt`→`Alt+P`** (WPF `Key.System`). Detalhe em `research/03-hotkeys-tray-popup.md`. Desbloqueia 07, 08.
- [Persistência + busca](issues/04-research-persistencia-busca.md): **`Microsoft.Data.Sqlite`** (EF rejeitado); upsert `ON CONFLICT` bumpeando datetime; `clear`/`deleteOldest` com proteção; metadata `System.Text.Json` (corrompido→null); imagens/cache em `%LocalAppData%` + GC órfãos; SQLite+Memory-testes, JSON fora; **`Sync Primary` N/A permanente**. Detalhe em `research/04-persistencia-busca.md`. Aberto p/ spec: STRICT, formato datetime, fusão track().
- [Ações + incognito + previews](issues/05-research-acoes-incognito-previews.md): actions.json 1:1 em `%AppData%`, regex .NET documentado + timeout 2s; libs **QRCoder 1.8.0 / AngleSharp 1.7.2 / AvalonEdit 6.3.1** (todas MIT); incognito em memória; `SessionEnding` cleanup <1s + CLI `--clear/--clear-all`; colar `Ctrl+V` default; preview HttpClient 5s; sons v1 `MediaPlayer`; **toast bloqueado até AUMID** → balão tray; pipe `Local\WindowsCM.<sid>`. Detalhe em `research/05-acoes-incognito-previews.md`.
- [Configs + customizações](issues/06-grilling-configs-customizacoes.md): cortes (Yaru, WM_CLASS→processo, indicator-display; Dependencies→Sobre/Diagnóstico); profiles Default+Compact; History UI só-SQLite; **UI inglês v1**, item/entry em `CONTEXT.md` (7 termos); 9 sons; 6 telas por-tipo; first-run Default; middle-click pin + toggle + swaps mantidos. Sem ADRs.
- [Protótipo popup](issues/07-prototype-popup-estilo.md): veredito **A (Cards)**; spec leva densidade Copyous 1:1, **Dark**, sem vazios verticais, largura cheia. Asset na branch `prototype/popup-wpf` (`a314624` + `817f0c2`). B/C descartadas.
- [Distribuição + licença](issues/08-grilling-distribuicao-licenca.md): Inno per-user + autostart opt-in + uninstall preserva dados; GPL-3.0 (LICENSE + SPDX + Sobre com créditos); **readiness confirmado — mapa done, handoff para `to-spec` autorizado**.

## Not yet specified

_(vazio — fog zerado em 07; o que resta é `08`, já tickado.)_

## Out of scope

- Sync nuvem / multi-device.
- Plugins de terceiros / store de extensões.
- MSIX/Store dia 1 (adiado para pós-MVP da spec).
- Port para macOS/Linux (só Windows neste esforço).
- Caret-UIA cursor mode (cursor-first v1 confirmado em 03/07; caret global vira v2 pós-spec).
- Toast real (bloqueado até installer com AUMID; balão tray na v1 — ver 05).

## Out of scope

- Sync nuvem / multi-device.
- Plugins de terceiros / store de extensões.
- MSIX/Store dia 1 (adiado para pós-MVP da spec).
- Port para macOS/Linux (só Windows neste esforço).
