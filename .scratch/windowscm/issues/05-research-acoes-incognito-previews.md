# Ações + incognito + colar + previews

Type: research
Status: resolved
Blocked by: 01

## Question

Portar ações customizáveis + incognito + colar + previews/sons com paridade.

Definir:
- Modelo `actions.json` portado (`command/color/qrcode`, `pattern/types/output/shortcut`, submenus, `defaults` por tipo) + path `%AppData%/WindowsCM/actions.json`, CRUD + Reset/Restore Built-In
- Defaults portados: `open-with-default`, `open-with-files`, `open-with-browser`, `paste-as-path`, conversões de cor, `qrcode (Ctrl+Q)` → comandos Windows (`ShellExecute`, `cut -c8-` equivalente?)
- Incognito (toggle, persistência?, indicador), `ClearHistory(all)` + auto-limpeza em restart/logout/shutdown Windows
- Colar: `SendInput` no app focado, `Shift+Insert` vs `Ctrl+V`, terminal? timing
- Link preview (`HttpClient` + parse og:/twitter:/title + cache imagem), code highlight local (qual lib WPF?), sons (`System.Media`, lista + volume), notificações/toast, wiggle equivalente
- CLI/IPC local equivalente ao DBus (`Toggle/Show/Hide/ClearHistory`) — named pipe? args?

Só começar quando `01` estiver `resolved`.

## Answer

Achados em `.scratch/windowscm/research/05-acoes-incognito-previews.md` (só fontes primárias; 10 seções, cada lib com nome+versão+licença+link).

Load-bearing:
- `actions.json` em `%AppData%\WindowsCM`, schema 1:1 (command/color/qrcode, submenu, defaults), shortcut `Ctrl+Q`. **Regex .NET ≠ JS (`\w` casa Unicode)** — documentar, nunca `ECMAScript` por default; todo `new Regex` com timeout 2s + `catch ArgumentException` = no-match (anti-ReDoS).
- Opens via `Process.Start{UseShellExecute=true}` (default false no .NET 8!); `explorer /select,"path"` v1; `paste-as-path` = strip `file://` + `Uri.UnescapeDataString`; comando roda `cmd.exe /c` com stdin, timeout 30s, `$1`→`%1`.
- Libs trancadas: **QRCoder 1.8.0 MIT** (ZXing.Net rejeitado), **AngleSharp 1.7.2 MIT** (HAP rejeitado), **AvalonEdit 6.3.1.120 MIT** read-only (ColorCode/hljs-WebView2 rejeitados; fallback plain-text como o original).
- Incognito em memória, não persistido; `prevClipboard`-antes-`shouldSave` preservado. `ClearHistory`: `SessionEnding` (cancelável, sem garantia; cleanup <1s, nunca `Cancel=true`; desassinar evento static) + CLI `--clear` (mantém protegidos) / `--clear-all` (tudo).
- Colar default `Ctrl+V` SendInput (decisão trancada, fluxo de 02); `Shift+Insert` opt-in; terminal adiado.
- Preview: `HttpClient` singleton, timeout 5s, UA próprio, só `text/html` vira metadata; cache `MD5(url)`; offline = sem preview.
- Sons: `SoundPlayer` só .wav sem volume → **v1 `MediaPlayer`** (`Volume=10^(dB/20)`; converter 8 oggs); NAudio 2.3.0 só se +dB real.
- **Toast real bloqueado até installer com AUMID** → v1 balão `NotifyIcon.ShowBalloonTip`; wiggle = popup-scale + flash do ícone 3×65ms.
- IPC: pipe `Local\WindowsCM.<sid>` + `CurrentUserOnly`, protocolo linha `toggle/show/hide/clear/clear-all`.
- Lacunas (spikes): mapa `language.id` hljs→AvalonEdit, audibilidade dos wavs, AUMID/MSI, terminal+delay (herdado 02).
