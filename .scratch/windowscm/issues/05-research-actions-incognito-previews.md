# Actions + incognito + paste + previews

Type: research
Status: resolved
Blocked by: 01

## Question

Port customizable actions + incognito + paste + previews/sounds with parity.

Define:
- Ported `actions.json` model (`command/color/qrcode`, `pattern/types/output/shortcut`, submenus, per-type `defaults`) + path `%AppData%/WindowsCM/actions.json`, CRUD + Reset/Restore Built-In
- Ported defaults: `open-with-default`, `open-with-files`, `open-with-browser`, `paste-as-path`, color conversions, `qrcode (Ctrl+Q)` → Windows commands (`ShellExecute`, a `cut -c8-` equivalent?)
- Incognito (toggle, persistence?, indicator), `ClearHistory(all)` + auto-clear on Windows restart/logout/shutdown
- Paste: `SendInput` into the focused app, `Shift+Insert` vs `Ctrl+V`, terminal? timing
- Link preview (`HttpClient` + parse og:/twitter:/title + image cache), local code highlight (which WPF lib?), sounds (`System.Media`, list + volume), notifications/toast, wiggle equivalent
- Local CLI/IPC equivalent to DBus (`Toggle/Show/Hide/ClearHistory`) — named pipe? args?

Only start when `01` is `resolved`.

## Answer

Findings in `.scratch/windowscm/research/05-actions-incognito-previews.md` (primary sources only; 10 sections, every lib with name+version+license+link).

Load-bearing:
- `actions.json` in `%AppData%\WindowsCM`, 1:1 schema (command/color/qrcode, submenu, defaults), shortcut `Ctrl+Q`. **.NET regex ≠ JS (`\w` matches Unicode)** — document it, never `ECMAScript` by default; every `new Regex` with a 2s timeout + `catch ArgumentException` = no-match (anti-ReDoS).
- Opens via `Process.Start{UseShellExecute=true}` (default false on .NET 8!); `explorer /select,"path"` v1; `paste-as-path` = strip `file://` + `Uri.UnescapeDataString`; the command runs `cmd.exe /c` with stdin, timeout 30s, `$1`→`%1`.
- Locked libs: **QRCoder 1.8.0 MIT** (ZXing.Net rejected), **AngleSharp 1.7.2 MIT** (HAP rejected), **AvalonEdit 6.3.1.120 MIT** read-only (ColorCode/hljs-WebView2 rejected; plain-text fallback like the original).
- Incognito in memory, not persisted; `prevClipboard`-before-`shouldSave` preserved. `ClearHistory`: `SessionEnding` (cancelable, no guarantee; cleanup <1s, never `Cancel=true`; unsubscribe the static event) + CLI `--clear` (keeps protected items) / `--clear-all` (everything).
- Default paste `Ctrl+V` SendInput (locked decision, flow from 02); `Shift+Insert` opt-in; terminal deferred.
- Preview: `HttpClient` singleton, timeout 5s, own UA, only `text/html` becomes metadata; cache `MD5(url)`; offline = no preview.
- Sounds: `SoundPlayer` is .wav only with no volume → **v1 `MediaPlayer`** (`Volume=10^(dB/20)`; convert 8 oggs); NAudio 2.3.0 only if real +dB.
- **Real toast blocked until an installer with AUMID** → v1 `NotifyIcon.ShowBalloonTip` balloon; wiggle = popup-scale + icon flash 3×65ms.
- IPC: pipe `Local\WindowsCM.<sid>` + `CurrentUserOnly`, line protocol `toggle/show/hide/clear/clear-all`.
- Gaps (spikes): hljs→AvalonEdit `language.id` map, audibility of the wavs, AUMID/MSI, terminal+delay (inherited from 02).
