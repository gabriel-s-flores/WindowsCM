# 05 — Ações + incognito + colar + previews/sons/toast + IPC (porte Windows)

Ticket: `.scratch/windowscm/issues/05-research-acoes-incognito-previews.md` (não editado).
Base portada (não re-auditada): `.scratch/windowscm/research/01-parity-inventory.md` §§4 (ações) e 8 (dependências) + `.scratch/windowscm/research/02-clipboard-core.md` (colar/single-instance).
Método: só fontes primárias — Microsoft Learn + docs oficiais das libs (GitHub/LICENSE + NuGet). Cada claim de API cita o Learn; cada lib traz nome+versão+licença+link oficial.

## 1. Modelo `actions.json` portado

### 1.1 Path
- `%AppData%\WindowsCM\actions.json` = `Environment.GetFolderPath(SpecialFolder.ApplicationData)` + `WindowsCM\actions.json`.
  `ApplicationData` (26) = roaming-user repo; `LocalApplicationData` (28) = non-roaming. Escolher `ApplicationData` preserva a semântica do `getConfigPath` XDG do original.
  [GetFolderPath](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath?view=net-10.0) · [SpecialFolder](https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=net-10.0)
- Criar dir se ausente; escrita atômica (tmp + move) + indentação com tab (paridade com `actions.ts:294`); manter flag `backup` antes de Reset/Restore.

### 1.2 Schema (porta 1:1 de `01 §4`)
```jsonc
{
  "actions": [
    { "kind": "command", "id": "<uuid>", "name": "...", "command": "cmd...",
      "pattern": "^(.*)", "types": ["File"], "output": "copy|paste|ignore", "shortcut": "Ctrl+Q" },
    { "kind": "color", "id": "<uuid>", "name": "...", "space": "hex",
      "types": ["Color"], "output": "copy|paste" },
    { "kind": "qrcode", "id": "<uuid>", "name": "...",
      "types": [], "output": "ignore", "shortcut": "Ctrl+Q" },
    { "name": "Open", "actions": [ /* submenu aninhado, mesmo shape */ ] }
  ],
  "defaults": { "File": "<id>", "Files": "<id>", "Link": "<id>" }
}
```
- `kinds`: `command` (+`command`), `color` (+`space`, `types:[Color]`, `output:copy|paste`), `qrcode` (`output:ignore` fixo). Submenu = `{name, actions}` recursivo. `types: []`/nulo = todos os 8 `ItemType` (Text, Code, Image, File, Files, Link, Character, Color — `01 §1`).
- `output`: `ignore|copy|paste` (comando); cor só `copy|paste`; QR só `ignore`.
- `shortcut`: string WPF (`Ctrl+Q`) em vez de `<Control>q` GTK; parse para `KeyGesture`. Default `qrcode` = `Ctrl+Q`.
- `defaults`: `Partial<Record<ItemType, id>>`; UI de Default Actions tem 1 linha por tipo (8) com opção None (`01 §4`).
- Desserializar com `System.Text.Json` (`JsonSerializerOptions{PropertyNameCaseInsensitive=true, ReadCommentHandling=Skip, AllowTrailingCommas=true}`); desconhecidos → ignorar (forward-compat).

### 1.3 Regex .NET vs JS RegExp — diferenças a documentar no spec
- Motor: `System.Text.RegularExpressions.Regex.IsMatch(input, pattern)` / `Match()` para grupos. Original usa `new RegExp(pattern)` + `test`/`match` (`01 §4`).
  [Regex.IsMatch](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.ismatch?view=net-10.0) · [Regex.Match](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.match?view=net-9.0)
- Diferenças load-bearing:
  1. **Semântica default ≠ ECMAScript.** .NET default = canônico (Unicode: `\w` ≈ `[\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}\p{Pc}\p{Lm}]`); JS = ASCII (`[A-Za-z0-9_]`). `RegexOptions.ECMAScript` aproxima do JS mas só combina com `IgnoreCase|Multiline|Compiled` e muda classe de caracteres/backreference. Não ativar por default — documentar que `^(?!rgb)` etc. continuam válidos, mas `\w/\b/\d` casam mais que no JS. [Regular-Expression-Options / ECMAScript](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options)
  2. **Timeout obrigatório.** Patterns vêm do usuário (actions.json editável) → sempre construir `new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2))` e tratar `RegexMatchTimeoutException` como *no-match*. Recomendação oficial: timeout ~2 s; patterns não-confiáveis exigem timeout. [Best-Practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-regex) · [MatchTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matchtimeout?view=net-9.0)
  3. **Regex inválida = sem match** (paridade): `try/catch (ArgumentException)` no `new Regex` → `testAction=false`, `matchAction=null` (`01 §4`: `:91-93`, `:110-112`).
  4. **Grupos:** `match.Groups[1..]` (índice 1-based; `Groups[0]` = match inteiro) — equivalente aos `grupos...` passados após `_` no `sh -c` original.
- Matching portado: filtro `types` primeiro (vazio = todos) → `IsMatch` com timeout → `isDefaultAction`/`findDefaultAction` por `defaults[entry.type]` (`01 §4`).

### 1.4 CRUD + Reset/Restore + reload
- CRUD na prefs espelha `actionsPage.ts`: Command exige nome+comando; Color exige nome+espaço (10 espaços); QR exige nome; ids novos = `Guid.NewGuid().ToString()` (porte de `uuid_string_random`).
- Restore = merge built-ins faltantes **por id**, preserva customs + `defaults` do usuário; Reset = volta a `defaultConfig` integral; ambos com backup (`01 §4`: merge `:345`, badge `countDifference` `:329`).
- Reload ao vivo: `FileSystemWatcher` (NotifyFilters.LastWrite|Size, `InternalBufferSize=4KB`, debounce ~250 ms, retry open 5× pois editor segura lock) substitui `FileMonitor` Gio (`01 §4` `:131-137`). [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0)

## 2. Defaults Windows + execução de comando

### 2.1 Tabela de porte (de `01 §4` defaults + `01 §8` shell defaults)
| id original | comando GNOME | porte Windows | API |
|---|---|---|---|
| `open-with-default` (`xargs xdg-open`, Image+File) | abre app padrão | `Process.Start(new ProcessStartInfo(path){UseShellExecute=true})`; verbo default (`lpVerb=NULL` = comando default do tipo) | [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0) · [Launching-Applications ShellExecute](https://learn.microsoft.com/en-us/windows/win32/shell/launch) · [Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-9.0) |
| `open-with-files` (`nautilus -s $1`, pattern `^(.*)`) | revela no gerenciador | `explorer.exe /select,"<path>"` (v1); ideal `SHOpenFolderAndSelectItems` via P/Invoke (sem processo filho) em v1.1 | [SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems) (requer `CoInitialize/Ex` antes) |
| `open-with-browser` (`xargs xdg-open`, Link) | abre browser default | `Process.Start(new ProcessStartInfo(url){UseShellExecute=true})` — abre browser default registrado | [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-7.0) ("any registered file type … default open action") |
| `paste-as-path` (`cut -c8-`, paste, Image+File+Files) | cola path sem `file://` | `path = Uri.UnescapeDataString(s.Replace("file://","").Trim().Trim('/'))`; Files = um por linha; `output:paste` | [Uri.UnescapeDataString](https://learn.microsoft.com/en-us/dotnet/api/system.uri.unescapedatastring?view=net-10.0) (não converte `+`→espaço — correto para paths) |
| conversões de cor (rgb/hex/hsl/oklch + hwb/linear/xyz/lab/lch/oklab comentados) | `Color.toColor(space)` | reusar **parser portado de 01** (`color.ts:160-174`, `:329-343`); mesma lista ativa/comentada, mesmos `^(?!…)` guards, `output:paste`, `shortcut:[]` | — (lógica portada, sem API nova) |
| `qrcode` (Text+Code+Link+Character+Color, ignore, `Ctrl+Q`) | diálogo QR | diálogo WPF + lib abaixo; atalho `Ctrl+Q` (`KeyGesture`) | — |

- Nota `UseShellExecute`: default `true` no .NET Framework, **`false` no .NET Core/5+** — setar explicitamente `true` nos 3 opens. `UseShellExecute=false` é obrigatório para redirect stdin/stdout (§2.2) e proíbe abrir documentos. [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0)
- Nota `explorer /select,`: switch documentado (`/select,object` abre view com objeto selecionado); path com espaços exige quoting `/select,"C:\…"`; alternativa programática `SHOpenFolderAndSelectItems` evita spawn e trata multi-select via PIDLs.

### 2.2 Execução de `command` (porte de `actionMenu.ts:199-232`)
- Original: `sh -c <command> _ <grupos...>` com **stdin = conteúdo**, timeout **30 s**, stdout `trim`, vazio = ignora, `copy|paste` emitem sinais (`01 §4`).
- Porte:
  ```csharp
  var psi = new ProcessStartInfo {
      FileName = "cmd.exe", Arguments = "/c " + action.Command + " " + string.Join(" ", groups),
      UseShellExecute = false, RedirectStandardInput = true,
      RedirectStandardOutput = true, RedirectStandardError = true,
      CreateNoWindow = true, StandardInputEncoding = Encoding.UTF8,
      StandardOutputEncoding = Encoding.UTF8 };
  ```
  escrever `content` no `StandardInput` e fechar; `WaitForExit(30_000)` else `Kill(entireProcessTree:true)`; stdout `Trim()`, vazio = ignora; `copy` = set clipboard, `paste` = set + SendInput (§5).
  [RedirectStandardOutput](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput?view=net-10.0) · [StandardOutput](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.standardoutput?view=net-10.0) · [WaitForExit(Int32)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0)
- Armadilhas documentadas no Learn (trancar no spec):
  - `Redirect*=true` exige `UseShellExecute=false`, senão exceção.
  - Deadlock: **ler stdout/stderr antes/de forma assíncrona**, nunca `WaitForExit()` antes de `ReadToEnd()` com filho verboso; usar `BeginOutputReadLine/BeginErrorReadLine` + `WaitForExit(timeout)` + `WaitForExit()` sem arg pós-`true` para drenar handlers.
  - `$1` (sh) **vira `%1` (cmd)** — comandos built-in que usam `$1` (`nautilus -s $1`) precisam de reescrita no `defaultConfig` Windows; grupos extras = `%2…`. Variáveis de ambiente: via `psi.Environment` (não herdar cegamente) + `WorkingDirectory` explícito.
- Timeout 30 s preservado como constante (`ActionTimeoutMs = 30_000`).

### 2.3 QR — UMA lib recomendada
- **Recomendada: QRCoder — v1.8.0 — MIT — [GitHub](https://github.com/Shane32/QRCoder) ([LICENSE.txt MIT](https://github.com/Shane32/QRCoder/blob/master/LICENSE.txt)) · [NuGet](https://www.nuget.org/packages/QRCoder/)** (puro-QR, sem System.Drawing obrigatório no core, `QRCodeGenerator` → `BitmapByteQRCode`/`PngByteQRCode` encaixa direto em `Image` WPF via `BitmapImage`; mantido, 1.8.0 em 04/2026).
- **Rejeitada: ZXing.Net — v0.16.11 — Apache-2.0 — [GitHub](https://github.com/micjahn/ZXing.Net/) · [NuGet](https://www.nuget.org/packages/ZXing.Net)** — barcode geral (decodifica+gera N formatos), superfície maior, release train mais lento; só faria sentido se spec pedisse *leitura* de QR de imagens do histórico (não pede — original só *gera*).
- Render v1: `PngByteQRCode.GetGraphic(pixelsPerModule:20)` → `BitmapImage` (sem escrita em disco); diálogo mostra + botão Copy (PNG no clipboard) — `output:ignore` preservado (ação não injeta texto).

## 3. Incognito

- **Estado em memória, não persistido.** `01` não lista nenhuma chave gschema para incognito (todas as chaves de Behavior/Feedback estão inventariadas e nenhuma é incognito) e `shouldSave` lê flag runtime (`clipboard.ts:236`); portanto o original é transiente por sessão. Porte: `bool _incognito` no serviço de clipboard, default `false`, resetado a cada launch.
- **Toggle:** hotkey global de 03 (`toggle-incognito-mode-shortcut`, default GNOME `Super+Control+Shift+V` → mapear em 03) + toggle no header/footer do popup + item no menu do tray. Todos chamam o mesmo `ToggleIncognito()`.
- **Indicador visual (2 superfícies):** (a) popup: chip/badge "Incognito" no header + tint escura nos cards (paridade com classe CSS do original); (b) tray: overlay no ícone (cadeado/ponto) + tooltip "WindowsCM — incognito on". Nenhum toast por toggle (evita vazar que o usuário entrou em modo privado no Action Center).
- **Regra anti-vazamento (preservar, de `02`/`01 §2`):** `prevClipboard=[type,checksum]` é setado **antes** de `shouldSave`, de modo que cópia feita em incognito atualiza o dedup e não vaza no evento seguinte ao desligar o modo (`clipboard.ts:269-274`). Porte literal: no handler `WM_CLIPBOARDUPDATE`, computar checksum → atualizar `prevClipboard` → só então checar `if (_incognito) return;`.

## 4. `ClearHistory(all)` + auto-limpeza em restart/logout/shutdown

- **Semântica (de `01 §3`+`§6`):** `ClearHistory(all:boolean)`; `all=true` → `Clear` (tudo); `false`/`-1`(auto) → pref `clipboard-history` (`KeepAll=nada | KeepPinnedAndTagged | Clear`). DBus `ClearHistory(in b all)` (`dbus.ts:7`); auto-limpeza via `ConfirmedLogout/Reboot/Shutdown` (SessionManager) + `PrepareForShutdown` (login1) → `emit('clear-history', -1)` → pref atual (`extension.ts:105`).
- **Porte Windows:**
  - `Microsoft.Win32.SystemEvents.SessionEnding` (cancelável, `Cancel=true` só *pede* continuação, sem garantia) para o caso logout/restart/shutdown iniciado pelo usuário; `SystemEvents.SessionEnded` (pós-fato, só registra) como best-effort.
    [SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionending?view=windowsdesktop-10.0) · [SessionEnded](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionended?view=windowsdesktop-10.0)
  - **Limitações a trancar no spec:**
    1. `SessionEnding/Ended` **só disparam com message pump rodando**; console apps não levantam; em serviço seria preciso hidden-form. App WPF (`Application.Run`) tem pump — OK sem janela oculta, mas o handler deve rodar no thread UI/dispatcher. (Learn SessionEnding remarks.)
    2. **Tempo curto:** `WM_QUERYENDSESSION` → app deve retornar TRUE/FALSE imediatamente e adiar cleanup para `WM_ENDSESSION`. Janela de graça típica: ~5 s até o diálogo de bloqueio, ~30 s para concluir após TRUE; shutdown crítico (`ENDSESSION_CRITICAL`) não pode ser bloqueado. Não fazer I/O pesado; `ClearHistory` deve ser síncrono e <1 s (delete SQL + apaga thumbs do escopo). [WM_QUERYENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession) · [Shutdown-Changes-Vista](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms700677(v=vs.85))
    3. **Nunca cancelar** (`e.Cancel=true`) para reter histórico — respeitar intenção do usuário (Learn: default `DefWindowProc` retorna TRUE); só limpar e sair.
    4. `SessionEnding` é **static** — desassinar no dispose (`SystemEvents.SessionEnding -= …`) ou vaza. (Learn remarks.)
- Mapeamento CLI/IPC: `--clear` (= `false`: mantém pinned+tagged) e `--clear-all` (= `true`: tudo), ver §10.

## 5. Colar — decisão trancada (fluxo de `02`, sem re-pesquisa)

Fluxo normativo de `02 §Colar`: capturar alvo `GetForegroundWindow()` no hotkey → esconder UI e devolver foco (sem confiar em `SetForegroundWindow` — sem direito = só pisca taskbar) → assert `GetForegroundWindow()==alvo` → `SendInput` → delay pós-foco tunável.
[GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow) · [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) · [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

- **Default: `Ctrl+V`** (`Ctrl↓ V↓ V↑ Ctrl↑` via `SendInput`, checando `GetAsyncKeyState` de Ctrl/Shift antes). Motivo: funciona em 99% dos apps Win32/WPF/WinForms/browser; `Shift+Insert` falha em apps que não mapeiam CUA e cola *PRIMARY-like* em nada no Windows.
- **`Shift+Insert` = opt-in configurável** (`paste-sequence: ctrlV|shiftInsert`, default `ctrlV`), não auto-detecção.
- **Heurística de terminal (ConHost `ConsoleWindowClass` / `WindowsTerminal.exe` via `GetWindowThreadProcessId`+nome) ADIADA** como fallback configurável pós-v1 com spike empírico — lacuna `02-§Lacunas-1` explicitamente não re-pesquisada aqui; key bindings de terminal são user-configuráveis então qualquer heurística seria chute.
- Timing: constante `PasteDelayMs` default **200 ms** (faixa 100–300, tunável), não os 250 ms literais do GNOME (`02 §Duvidosas`: sem prescrição no Windows; `SendInput` é serial sem intercalação). UIPI: contra app elevado, paste falha **silencioso** (sem `GetLastError`) — documentar "rode como admin se alvo for elevado".

## 6. Link preview

- **Transporte:** `HttpClient` singleton estático reutilizado (guideline: reusar instâncias no ciclo de vida), `Timeout = 5 s` (paridade com `idle_timeout:5` do Soup, `01 §8`; default .NET seria 100 s — longo demais para hover/cards), `DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; WindowsCM/1.0; +https://github.com/<org>/WindowsCM)")` (porte do `CopyousBot/1.0`), `Accept: text/html`, `GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts)` + `CancellationTokenSource` cancelado ao trocar de seleção/scroll-out.
  [HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-10.0) · [Timeout](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-10.0) · [Make-HTTP-requests](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient) (reuso + `DefaultRequestHeaders.UserAgent` + cancelamento/timeout)
- **Classificação (paridade `01 §8`):** só `Content-Type: text/html` vira metadata; `image/*` direto vira `{image:url}` + cache; resto = sem preview. Títulos: `og:title|twitter:title|<title>`, descrição idem, imagem `og:image*|twitter:image` + resolve relativo (`new Uri(base, rel)` = porte de `parse_relative`).
- **Parser — UM recomendado:**
  - **Recomendado: AngleSharp — v1.7.2 estável (1.8.0 em beta 09/2026) — MIT — [GitHub](https://github.com/AngleSharp/AngleSharp) ([LICENSE MIT](https://github.com/AngleSharp/AngleSharp/blob/master/LICENSE)) · [Docs](https://anglesharp.github.io/) · [Releases](https://github.com/AngleSharp/AngleSharp/releases)** — parser HTML5/CSS da spec W3C, DOM + `QuerySelector("meta[property='og:title']")`, tolerante a HTML quebrado; `BrowsingContext` desacoplado de rede (alimentamos string do HttpClient — sem fetch duplo).
  - Rejeitado: **HtmlAgilityPack — v1.13.0 — MIT — [GitHub](https://github.com/zzzprojects/html-agility-pack) · [NuGet](https://www.nuget.org/packages/HtmlAgilityPack/)** — XPath/leniente e popular, mas DOM fora da spec, sem CSS, manutenção por vendor (ZzzProjects); só venceria se spec exigisse XPath legado.
- **Cache imagem:** `MD5(url)` em `%LocalAppData%\WindowsCM\Cache\link-images\` (porte de `getCachePath` + `MD5(url)`); hit pula download; limite 50 MB LRU simples; respeitar `show-link-preview(-image)`, bg, orientação e **exclusion regex[]** (mesmo motor `Regex` com timeout do §1.3).
- **Offline:** qualquer `HttpRequestException`/`TaskCanceledException` → item sem preview, sem retry, sem toast (paridade: preview é best-effort).

## 7. Code highlight em WPF — UM caminho v1

- **Recomendado v1: AvalonEdit — v6.3.1.120 — MIT — [GitHub](https://github.com/icsharpcode/AvalonEdit) ([LICENSE MIT](https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE)) · [NuGet](https://www.nuget.org/packages/AvalonEdit) · [Site](http://avalonedit.net/)** — componente WPF nativo (SharpDevelop/ILSpy), `TextEditor{IsReadOnly=true, SyntaxHighlighting=HighlightingManager.GetDefinitionByExtension(lang)}`, dezenas de highlightings `.xshd` embutidos, folding disponível (não usar nos cards), sem runtime extra, sem bridge JS. Mapear `language.id` do classificador (porte `highlightAuto`) para extensão/definição; desconhecido → plain-text.
- Rejeitados:
  - **ColorCode-Universal — Core/HTML v2.0.15 — licença `Other` (não MIT limpo) — [GitHub](https://github.com/CommunityToolkit/ColorCode-Universal) · [NuGet](https://www.nuget.org/packages/ColorCode.HTML)** — set pequeno de linguagens, formatter HTML/UWP, exigiria formatter WPF custom; licença ambígua para bundle.
  - **highlight.js via WebView2** (original usa hljs 11.11.1, `01 §1`) — peso: runtime Evergreen WebView2 (~100 MB+, deploy/boot extra) + bridge assíncrona por card + 192 pacotes de idioma; overkill para lista virtualizada; fora do v1.
- **Fallback sem highlight (paridade):** texto escapado `TextBlock` monospace, como o original faz sem hljs (`codeLabel.ts:382`, `01 §8`).

## 8. Sons

Lista original (8, `01 §8`): `click, hum, string, swing, message, message-new-instant, bell, dialog-warning` (.ogg GNOME em `<datadir>/sounds/gnome/default/alerts/`). Pref original: `sound` enum + `volume -20…+20 dB` default 0.

- **Fato Learn:** `System.Media.SoundPlayer` **só toca `.wav`** (path/URL/Stream/recurso), com `Play/PlaySync/LoadAsync`; outros tipos (`.mp3/.wma/.ogg`) = não suportado (usar MediaPlayer).
  [SoundPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.media.soundplayer?view=windowsdesktop-10.0) · [Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/soundplayer-class-overview)
- **Consequência:** os 8 `.ogg` GNOME **precisam ser convertidos para `.wav` (44.1 kHz/16-bit PCM)** e empacotados como `Resource` (ou `Content` em `%ProgramFiles%`); mapear nomes 1:1.
- **Volume:** `SoundPlayer` **não tem propriedade de volume** (toca no volume do sistema) → slider `-20…+20 dB` **inviável** com ele. Duas rotas:
  - (a) **v1 recomendada — sem NAudio:** `System.Windows.Media.MediaPlayer` (`Volume 0…1` linear, `Open(Uri)+Play()`, toca mp3/wma/wav via WMP; manter referência viva senão GC para o áudio). Mapear dB→linear: `gain = 10^(dB/20)` clamp `0…1` (0 dB = 0.5? Não — 0 dB = `Volume 1.0`; -20 dB ≈ 0.1; +20 dB clampa em 1.0 com nota "ganho >0 dB não suportado sem DSP"). [MediaPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.mediaplayer?view=windowsdesktop-9.0) · [Control-MediaElement-Volume](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-control-a-mediaelement-play-pause-stop-volume-and-speed)
  - (b) **Fiel-a-dB — com NAudio:** **NAudio — v2.3.0 (3.0 em preview) — MIT — [GitHub](https://github.com/naudio/NAudio) · [NuGet](https://www.nuget.org/packages/NAudio/)** (`AudioFileReader.Volume 0…1`, `WaveOutEvent`/WASAPI `WasapiOut`, `VolumeSampleProvider` aceita dB via `10^(dB/20)` sem clamp criativo). Custo: dependência nativa-ish maior + `AudioFileReader` não decodifica `.ogg` Vorbis out-of-box (precisaria NVorbis ou os `.wav` convertidos de qualquer forma).
- **Decisão:** v1 = `MediaPlayer` + wavs convertidos + slider mapeado para `0…1` com rótulo dB preservado na UI (paridade visual, fidelidade parcial documentada); NAudio só se spike mostrar gap audível ou se pedirem `+dB` real.

## 9. Notificações/toast + wiggle

- **Fato Learn (bloqueador do toast fiel):** app **unpackaged** (nosso caso v1, sem MSIX) pode mandar toast mas com passos especiais: declarar **AUMID** (`Company.App`) + **CLSID stub** no atalho Start (`System.AppUserModel.ID`, `System.AppUserModel.ToastActivatorCLSID`), chamar `RegisterAumidAndComServer(AUMID, clsid)` no startup, instalar via installer antes de debugar, e com stub só **protocol activation** funciona; **imagens http não suportadas em unpackaged** (baixar para app-data local). Sem isso, toast silenciosamente não aparece.
  [Toast-Desktop-Apps](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/toast-desktop-apps) · [Send-Local-Toast C#](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast) · [Migration-Guide Toolkit vs AppSDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/toast-notifications)
- **Libs:**
  - **Microsoft.Toolkit.Uwp.Notifications — v7.1.3 — MIT (.NET Foundation, header nos fontes) — [NuGet](https://www.nuget.org/packages/Microsoft.Toolkit.Uwp.Notifications/) · [ToastContentBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.toolkit.uwp.notifications.toastcontentbuilder?view=win-comm-toolkit-dotnet-7.1) · [Fonte](https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/Microsoft.Toolkit.Uwp.Notifications/Toasts/Builder/ToastContentBuilder.cs)** — `ToastContentBuilder().AddText(…).Show()` + `ToastNotificationManagerCompat.OnActivated`; caminho moderno alternativo = `Microsoft.WindowsAppSDK` `AppNotificationManager.Register()/Show()` (exige runtime WinAppSDK).
- **Decisão v1: balão do tray (`NotifyIcon.ShowBalloonTip`), toast adiado para o marco MSIX.** `NotifyIcon.ShowBalloonTip(timeout, title, text, icon)`: sem AUMID/instalador/COM, uma chamada; timeout hoje **deprecated** (duração pelo SO/acessibilidade, tipicamente 10–30 s impostas pelo OS); um balão por vez.
  [ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0) · [NotifyIcon-Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms)
  Mapear pref `send-notification` (default false, `01 §5`) para o balão; toast Toolkit entra quando houver installer com AUMID + ícone local.
- **Wiggle (indicador GNOME balança 2 px/65 ms×3, `01 §7`):** sem equivalente no tray. Porte = **(a)** animação de escala do popup na abertura (150 ms, paridade com animação do diálogo) **+ (b)** flash do ícone do tray (alternar `Icon` base/overlay 3×65 ms) a cada cópia nova quando `wiggle-indicator=true`. Sem shake de janela, sem som acoplado (som é pref separada).

## 10. IPC equivalente ao DBus (`Toggle/Show/Hide/ClearHistory`)

DBus original: iface `org.gnome.Shell.Extensions.Copyous`, métodos `Toggle, Show, Hide, ClearHistory(in b all)`, `ClearHistory(true)=tudo, false=mantém pinned+tagged` (`01 §6`).

- **Transporte: named pipe `System.IO.Pipes`.** Servidor na primeira instância (`NamedPipeServerStream`, `PipeDirection.InOut`, `PipeTransmissionMode.Byte`, `PipeOptions.Asynchronous|CurrentUserOnly`), loop `WaitForConnectionAsync` + `StreamReader/Writer` UTF-8 por linha.
  [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0) · [NamedPipeClientStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream?view=net-10.0) · [How-to-Named-Pipes](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication)
- **Nome + ACL (porte do handoff de `02`):** `Local\WindowsCM.<UserSid>` (sessão; `Local\` vs `Global\` em [Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0)); isolamento por `PipeOptions.CurrentUserOnly` (só mesmo usuário **e** mesmo nível de elevação) em vez de `PipeSecurity` custom — `MutexSecurity`/ACL em mutex nomeado **não existe no .NET Core/5+** (`02 §Duvidosas`). `NamedPipeServerStreamAcl.Create` com `PipeSecurity` só se auditoria exigir ACL explícita.
  [PipeOptions.CurrentUserOnly](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0) · [NamedPipeServerStreamAcl.Create](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create?view=net-10.0)
- **Single-instance (de `02`):** `new Mutex(false, @"Local\WindowsCM.<sid>", out createdNew)`; se `!createdNew` → `NamedPipeClientStream(".", pipename)` `Connect(2000)` → escreve comando → sai. Sem `MutexSecurity` no .NET 8.
- **Protocolo v1 — linha de texto minúscula** (não JSON): `toggle | show | hide | clear | clear-all | ping` + `\n`; resposta `ok\n` / `unknown\n`. Motivo: comandos não têm payload; framing por linha evita half-read de JSON; case-insensitive; desconhecido = `unknown` sem crash. (JSON só se v2 precisar de args como `copy <id>`.)
- **CLI:** `--toggle | --show | --hide | --clear | --clear-all` (`Environment.GetCommandLineArgs`). `--clear` = `ClearHistory(false)` (mantém pinned+tagged, = DBus `false`); `--clear-all` = `ClearHistory(true)` (tudo). Sem `--clear-pinned` separado (original não tem granularidade pinned-only; `KeepPinnedAndTagged` é atômico). Segunda instância com flag encaminha pela pipe e sai com código 0; sem servidor (stale mutex) → torna-se servidor.
