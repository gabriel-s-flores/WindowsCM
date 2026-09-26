# 05 — Actions + incognito + paste + previews/sounds/toast + IPC (Windows port)

Ticket: `.scratch/windowscm/issues/05-research-actions-incognito-previews.md` (not edited).
Ported baseline (not re-audited): `.scratch/windowscm/research/01-parity-inventory.md` §§4 (actions) and 8 (dependencies) + `.scratch/windowscm/research/02-clipboard-core.md` (paste/single-instance).
Method: primary sources only — Microsoft Learn + the libs' official docs (GitHub/LICENSE + NuGet). Every API claim cites Learn; every lib comes with name+version+license+official link.

## 1. Ported `actions.json` model

### 1.1 Path
- `%AppData%\WindowsCM\actions.json` = `Environment.GetFolderPath(SpecialFolder.ApplicationData)` + `WindowsCM\actions.json`.
  `ApplicationData` (26) = roaming-user repo; `LocalApplicationData` (28) = non-roaming. Choosing `ApplicationData` preserves the semantics of the original's XDG `getConfigPath`.
  [GetFolderPath](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath?view=net-10.0) · [SpecialFolder](https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=net-10.0)
- Create the dir if missing; atomic write (tmp + move) + tab indentation (parity with `actions.ts:294`); keep the `backup` flag before Reset/Restore.

### 1.2 Schema (1:1 port of `01 §4`)
```jsonc
{
  "actions": [
    { "kind": "command", "id": "<uuid>", "name": "...", "command": "cmd...",
      "pattern": "^(.*)", "types": ["File"], "output": "copy|paste|ignore", "shortcut": "Ctrl+Q" },
    { "kind": "color", "id": "<uuid>", "name": "...", "space": "hex",
      "types": ["Color"], "output": "copy|paste" },
    { "kind": "qrcode", "id": "<uuid>", "name": "...",
      "types": [], "output": "ignore", "shortcut": "Ctrl+Q" },
    { "name": "Open", "actions": [ /* nested submenu, same shape */ ] }
  ],
  "defaults": { "File": "<id>", "Files": "<id>", "Link": "<id>" }
}
```
- `kinds`: `command` (+`command`), `color` (+`space`, `types:[Color]`, `output:copy|paste`), `qrcode` (fixed `output:ignore`). Submenu = recursive `{name, actions}`. `types: []`/null = all 8 `ItemType` values (Text, Code, Image, File, Files, Link, Character, Color — `01 §1`).
- `output`: `ignore|copy|paste` (command); color only `copy|paste`; QR only `ignore`.
- `shortcut`: WPF string (`Ctrl+Q`) instead of GTK `<Control>q`; parsed into a `KeyGesture`. Default `qrcode` = `Ctrl+Q`.
- `defaults`: `Partial<Record<ItemType, id>>`; the Default Actions UI has 1 row per type (8) with a None option (`01 §4`).
- Deserialize with `System.Text.Json` (`JsonSerializerOptions{PropertyNameCaseInsensitive=true, ReadCommentHandling=Skip, AllowTrailingCommas=true}`); unknowns → ignore (forward-compat).

### 1.3 .NET Regex vs JS RegExp — differences to document in the spec
- Engine: `System.Text.RegularExpressions.Regex.IsMatch(input, pattern)` / `Match()` for groups. The original uses `new RegExp(pattern)` + `test`/`match` (`01 §4`).
  [Regex.IsMatch](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.ismatch?view=net-10.0) · [Regex.Match](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.match?view=net-9.0)
- Load-bearing differences:
  1. **Default semantics ≠ ECMAScript.** .NET default = canonical (Unicode: `\w` ≈ `[\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}\p{Pc}\p{Lm}]`); JS = ASCII (`[A-Za-z0-9_]`). `RegexOptions.ECMAScript` approximates JS but only combines with `IgnoreCase|Multiline|Compiled` and changes character classes/backreferences. Do not enable it by default — document that `^(?!rgb)` etc. remain valid, but `\w/\b/\d` match more than in JS. [Regular-Expression-Options / ECMAScript](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options)
  2. **Mandatory timeout.** Patterns come from the user (editable actions.json) → always construct `new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2))` and treat `RegexMatchTimeoutException` as *no-match*. Official recommendation: timeout ~2 s; untrusted patterns require a timeout. [Best-Practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-regex) · [MatchTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matchtimeout?view=net-9.0)
  3. **Invalid regex = no match** (parity): `try/catch (ArgumentException)` around `new Regex` → `testAction=false`, `matchAction=null` (`01 §4`: `:91-93`, `:110-112`).
  4. **Groups:** `match.Groups[1..]` (1-based index; `Groups[0]` = the whole match) — equivalent to the `groups...` passed after `_` in the original `sh -c`.
- Ported matching: `types` filter first (empty = all) → `IsMatch` with timeout → `isDefaultAction`/`findDefaultAction` by `defaults[entry.type]` (`01 §4`).

### 1.4 CRUD + Reset/Restore + reload
- CRUD in the prefs mirrors `actionsPage.ts`: Command requires name+command; Color requires name+space (10 spaces); QR requires a name; new ids = `Guid.NewGuid().ToString()` (port of `uuid_string_random`).
- Restore = merge missing built-ins **by id**, preserving customs + the user's `defaults`; Reset = back to the full `defaultConfig`; both with backup (`01 §4`: merge `:345`, badge `countDifference` `:329`).
- Live reload: `FileSystemWatcher` (NotifyFilters.LastWrite|Size, `InternalBufferSize=4KB`, debounce ~250 ms, retry open 5× because the editor holds a lock) replaces the Gio `FileMonitor` (`01 §4` `:131-137`). [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0)

## 2. Windows defaults + command execution

### 2.1 Port table (from `01 §4` defaults + `01 §8` shell defaults)
| original id | GNOME command | Windows port | API |
|---|---|---|---|
| `open-with-default` (`xargs xdg-open`, Image+File) | opens the default app | `Process.Start(new ProcessStartInfo(path){UseShellExecute=true})`; default verb (`lpVerb=NULL` = the type's default command) | [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0) · [Launching-Applications ShellExecute](https://learn.microsoft.com/en-us/windows/win32/shell/launch) · [Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-9.0) |
| `open-with-files` (`nautilus -s $1`, pattern `^(.*)`) | reveals it in the file manager | `explorer.exe /select,"<path>"` (v1); ideally `SHOpenFolderAndSelectItems` via P/Invoke (no child process) in v1.1 | [SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems) (requires `CoInitialize/Ex` first) |
| `open-with-browser` (`xargs xdg-open`, Link) | opens the default browser | `Process.Start(new ProcessStartInfo(url){UseShellExecute=true})` — opens the registered default browser | [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-7.0) ("any registered file type … default open action") |
| `paste-as-path` (`cut -c8-`, paste, Image+File+Files) | pastes the path without `file://` | `path = Uri.UnescapeDataString(s.Replace("file://","").Trim().Trim('/'))`; Files = one per line; `output:paste` | [Uri.UnescapeDataString](https://learn.microsoft.com/en-us/dotnet/api/system.uri.unescapedatastring?view=net-10.0) (does not convert `+`→space — correct for paths) |
| color conversions (rgb/hex/hsl/oklch + commented-out hwb/linear/xyz/lab/lch/oklab) | `Color.toColor(space)` | reuse the **parser ported from 01** (`color.ts:160-174`, `:329-343`); same active/commented-out list, same `^(?!…)` guards, `output:paste`, `shortcut:[]` | — (ported logic, no new API) |
| `qrcode` (Text+Code+Link+Character+Color, ignore, `Ctrl+Q`) | QR dialog | WPF dialog + the lib below; `Ctrl+Q` shortcut (`KeyGesture`) | — |

- `UseShellExecute` note: default `true` on .NET Framework, **`false` on .NET Core/5+** — explicitly set `true` in the 3 opens. `UseShellExecute=false` is mandatory for redirecting stdin/stdout (§2.2) and rules out opening documents. [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0)
- `explorer /select,` note: documented switch (`/select,object` opens a view with the object selected); a path with spaces requires quoting `/select,"C:\…"`; the programmatic alternative `SHOpenFolderAndSelectItems` avoids a spawn and handles multi-select via PIDLs.

### 2.2 `command` execution (port of `actionMenu.ts:199-232`)
- Original: `sh -c <command> _ <groups...>` with **stdin = content**, timeout **30 s**, stdout `trim`, empty = ignored, `copy|paste` emit signals (`01 §4`).
- Port:
  ```csharp
  var psi = new ProcessStartInfo {
      FileName = "cmd.exe", Arguments = "/c " + action.Command + " " + string.Join(" ", groups),
      UseShellExecute = false, RedirectStandardInput = true,
      RedirectStandardOutput = true, RedirectStandardError = true,
      CreateNoWindow = true, StandardInputEncoding = Encoding.UTF8,
      StandardOutputEncoding = Encoding.UTF8 };
  ```
  write `content` to `StandardInput` and close it; `WaitForExit(30_000)` else `Kill(entireProcessTree:true)`; stdout `Trim()`, empty = ignored; `copy` = set clipboard, `paste` = set + SendInput (§5).
  [RedirectStandardOutput](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput?view=net-10.0) · [StandardOutput](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.standardoutput?view=net-10.0) · [WaitForExit(Int32)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0)
- Pitfalls documented on Learn (lock them into the spec):
  - `Redirect*=true` requires `UseShellExecute=false`, otherwise an exception.
  - Deadlock: **read stdout/stderr first/asynchronously**, never `WaitForExit()` before `ReadToEnd()` with a verbose child; use `BeginOutputReadLine/BeginErrorReadLine` + `WaitForExit(timeout)` + an argument-less `WaitForExit()` after `true` to drain the handlers.
  - `$1` (sh) **becomes `%1` (cmd)** — built-in commands that use `$1` (`nautilus -s $1`) need rewriting in the Windows `defaultConfig`; extra groups = `%2…`. Environment variables: via `psi.Environment` (do not inherit blindly) + an explicit `WorkingDirectory`.
- 30 s timeout preserved as a constant (`ActionTimeoutMs = 30_000`).

### 2.3 QR — ONE recommended lib
- **Recommended: QRCoder — v1.8.0 — MIT — [GitHub](https://github.com/Shane32/QRCoder) ([LICENSE.txt MIT](https://github.com/Shane32/QRCoder/blob/master/LICENSE.txt)) · [NuGet](https://www.nuget.org/packages/QRCoder/)** (pure QR, no mandatory System.Drawing in the core, `QRCodeGenerator` → `BitmapByteQRCode`/`PngByteQRCode` plugs straight into a WPF `Image` via `BitmapImage`; maintained, 1.8.0 in 04/2026).
- **Rejected: ZXing.Net — v0.16.11 — Apache-2.0 — [GitHub](https://github.com/micjahn/ZXing.Net/) · [NuGet](https://www.nuget.org/packages/ZXing.Net)** — general barcode (decodes+generates N formats), larger surface, slower release train; would only make sense if the spec asked for *reading* QR codes from history images (it does not — the original only *generates*).
- Render v1: `PngByteQRCode.GetGraphic(pixelsPerModule:20)` → `BitmapImage` (no disk write); the dialog shows it + a Copy button (PNG to the clipboard) — `output:ignore` preserved (the action does not inject text).

## 3. Incognito

- **In-memory state, not persisted.** `01` lists no gschema key for incognito (all Behavior/Feedback keys are inventoried and none is incognito) and `shouldSave` reads a runtime flag (`clipboard.ts:236`); therefore the original is transient per session. Port: `bool _incognito` in the clipboard service, default `false`, reset on every launch.
- **Toggle:** global hotkey from 03 (`toggle-incognito-mode-shortcut`, GNOME default `Super+Control+Shift+V` → map in 03) + a toggle in the popup header/footer + an item in the tray menu. All call the same `ToggleIncognito()`.
- **Visual indicator (2 surfaces):** (a) popup: "Incognito" chip/badge in the header + a dark tint on the cards (parity with the original's CSS class); (b) tray: overlay on the icon (padlock/dot) + tooltip "WindowsCM — incognito on". No toast per toggle (avoids leaking in the Action Center that the user entered private mode).
- **Anti-leak rule (preserve, from `02`/`01 §2`):** `prevClipboard=[type,checksum]` is set **before** `shouldSave`, so a copy made in incognito updates the dedup and does not leak on the next event after the mode is turned off (`clipboard.ts:269-274`). Literal port: in the `WM_CLIPBOARDUPDATE` handler, compute the checksum → update `prevClipboard` → only then check `if (_incognito) return;`.

## 4. `ClearHistory(all)` + auto-clear on restart/logout/shutdown

- **Semantics (from `01 §3`+`§6`):** `ClearHistory(all:boolean)`; `all=true` → `Clear` (everything); `false`/`-1`(auto) → `clipboard-history` pref (`KeepAll=nothing | KeepPinnedAndTagged | Clear`). DBus `ClearHistory(in b all)` (`dbus.ts:7`); auto-clear via `ConfirmedLogout/Reboot/Shutdown` (SessionManager) + `PrepareForShutdown` (login1) → `emit('clear-history', -1)` → current pref (`extension.ts:105`).
- **Windows port:**
  - `Microsoft.Win32.SystemEvents.SessionEnding` (cancelable, `Cancel=true` only *requests* continuation, no guarantee) for the user-initiated logout/restart/shutdown case; `SystemEvents.SessionEnded` (after the fact, only records it) as best-effort.
    [SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionending?view=windowsdesktop-10.0) · [SessionEnded](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionended?view=windowsdesktop-10.0)
  - **Limitations to lock into the spec:**
    1. `SessionEnding/Ended` **only fire with a running message pump**; console apps do not raise them; a service would need a hidden-form. A WPF app (`Application.Run`) has a pump — OK without a hidden window, but the handler must run on the UI/dispatcher thread. (Learn SessionEnding remarks.)
    2. **Short time:** `WM_QUERYENDSESSION` → the app must return TRUE/FALSE immediately and defer cleanup to `WM_ENDSESSION`. Typical grace window: ~5 s until the blocking dialog, ~30 s to finish after TRUE; a critical shutdown (`ENDSESSION_CRITICAL`) cannot be blocked. No heavy I/O; `ClearHistory` must be synchronous and <1 s (SQL delete + deletes the in-scope thumbs). [WM_QUERYENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession) · [Shutdown-Changes-Vista](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms700677(v=vs.85))
    3. **Never cancel** (`e.Cancel=true`) to retain history — respect the user's intent (Learn: the default `DefWindowProc` returns TRUE); just clear and exit.
    4. `SessionEnding` is **static** — unsubscribe on dispose (`SystemEvents.SessionEnding -= …`) or it leaks. (Learn remarks.)
- CLI/IPC mapping: `--clear` (= `false`: keeps pinned+tagged) and `--clear-all` (= `true`: everything), see §10.

## 5. Paste — locked decision (flow from `02`, no re-research)

Normative flow from `02 §Pasting`: capture the target `GetForegroundWindow()` at hotkey time → hide the UI and hand focus back (without relying on `SetForegroundWindow` — no right = it only flashes the taskbar) → assert `GetForegroundWindow()==target` → `SendInput` → tunable post-focus delay.
[GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow) · [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) · [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

- **Default: `Ctrl+V`** (`Ctrl↓ V↓ V↑ Ctrl↑` via `SendInput`, checking `GetAsyncKeyState` for Ctrl/Shift first). Reason: works in 99% of Win32/WPF/WinForms/browser apps; `Shift+Insert` fails in apps that do not map CUA, and its *PRIMARY-like* paste applies to nothing on Windows.
- **`Shift+Insert` = configurable opt-in** (`paste-sequence: ctrlV|shiftInsert`, default `ctrlV`), not auto-detection.
- **Terminal heuristic (ConHost `ConsoleWindowClass` / `WindowsTerminal.exe` via `GetWindowThreadProcessId`+name) DEFERRED** as a post-v1 configurable fallback with an empirical spike — gap `02-§Gaps-1` explicitly not re-researched here; terminal key bindings are user-configurable, so any heuristic would be a guess.
- Timing: `PasteDelayMs` constant, default **200 ms** (range 100–300, tunable), not GNOME's literal 250 ms (`02 §Dubious`: no prescription on Windows; `SendInput` is serial without interleaving). UIPI: against an elevated app, paste fails **silently** (no `GetLastError`) — document "run as admin if the target is elevated".

## 6. Link preview

- **Transport:** reused static singleton `HttpClient` (guideline: reuse instances across the lifetime), `Timeout = 5 s` (parity with Soup's `idle_timeout:5`, `01 §8`; the .NET default would be 100 s — too long for hover/cards), `DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; WindowsCM/1.0; +https://github.com/<org>/WindowsCM)")` (port of `CopyousBot/1.0`), `Accept: text/html`, `GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts)` + `CancellationTokenSource` canceled on selection change/scroll-out.
  [HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-10.0) · [Timeout](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-10.0) · [Make-HTTP-requests](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient) (reuse + `DefaultRequestHeaders.UserAgent` + cancellation/timeout)
- **Classification (parity `01 §8`):** only `Content-Type: text/html` becomes metadata; a direct `image/*` becomes `{image:url}` + cache; the rest = no preview. Titles: `og:title|twitter:title|<title>`, description likewise, image `og:image*|twitter:image` + relative resolution (`new Uri(base, rel)` = port of `parse_relative`).
- **Parser — ONE recommended:**
  - **Recommended: AngleSharp — v1.7.2 stable (1.8.0 in beta 09/2026) — MIT — [GitHub](https://github.com/AngleSharp/AngleSharp) ([LICENSE MIT](https://github.com/AngleSharp/AngleSharp/blob/master/LICENSE)) · [Docs](https://anglesharp.github.io/) · [Releases](https://github.com/AngleSharp/AngleSharp/releases)** — HTML5/CSS parser per the W3C spec, DOM + `QuerySelector("meta[property='og:title']")`, tolerant of broken HTML; `BrowsingContext` decoupled from the network (we feed it the HttpClient string — no double fetch).
  - Rejected: **HtmlAgilityPack — v1.13.0 — MIT — [GitHub](https://github.com/zzzprojects/html-agility-pack) · [NuGet](https://www.nuget.org/packages/HtmlAgilityPack/)** — XPath/lenient and popular, but a non-spec DOM, no CSS, vendor maintenance (ZzzProjects); would only win if the spec required legacy XPath.
- **Image cache:** `MD5(url)` in `%LocalAppData%\WindowsCM\Cache\link-images\` (port of `getCachePath` + `MD5(url)`); a hit skips the download; simple 50 MB LRU limit; honor `show-link-preview(-image)`, bg, orientation and **exclusion regex[]** (same `Regex` engine with the timeout from §1.3).
- **Offline:** any `HttpRequestException`/`TaskCanceledException` → item without a preview, no retry, no toast (parity: preview is best-effort).

## 7. Code highlight in WPF — ONE v1 path

- **Recommended v1: AvalonEdit — v6.3.1.120 — MIT — [GitHub](https://github.com/icsharpcode/AvalonEdit) ([LICENSE MIT](https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE)) · [NuGet](https://www.nuget.org/packages/AvalonEdit) · [Site](http://avalonedit.net/)** — native WPF component (SharpDevelop/ILSpy), `TextEditor{IsReadOnly=true, SyntaxHighlighting=HighlightingManager.GetDefinitionByExtension(lang)}`, dozens of built-in `.xshd` highlightings, folding available (do not use it in the cards), no extra runtime, no JS bridge. Map the classifier's `language.id` (`highlightAuto` port) to an extension/definition; unknown → plain-text.
- Rejected:
  - **ColorCode-Universal — Core/HTML v2.0.15 — `Other` license (not clean MIT) — [GitHub](https://github.com/CommunityToolkit/ColorCode-Universal) · [NuGet](https://www.nuget.org/packages/ColorCode.HTML)** — small set of languages, HTML/UWP formatter, would require a custom WPF formatter; ambiguous license for bundling.
  - **highlight.js via WebView2** (the original uses hljs 11.11.1, `01 §1`) — weight: Evergreen WebView2 runtime (~100 MB+, extra deploy/boot) + an async bridge per card + 192 language packs; overkill for a virtualized list; out of v1.
- **Fallback without highlight (parity):** escaped text in a monospace `TextBlock`, as the original does without hljs (`codeLabel.ts:382`, `01 §8`).

## 8. Sounds

Original list (8, `01 §8`): `click, hum, string, swing, message, message-new-instant, bell, dialog-warning` (GNOME .ogg files in `<datadir>/sounds/gnome/default/alerts/`). Original pref: `sound` enum + `volume -20…+20 dB` default 0.

- **Learn fact:** `System.Media.SoundPlayer` **only plays `.wav`** (path/URL/Stream/resource), with `Play/PlaySync/LoadAsync`; other types (`.mp3/.wma/.ogg`) = not supported (use MediaPlayer).
  [SoundPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.media.soundplayer?view=windowsdesktop-10.0) · [Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/soundplayer-class-overview)
- **Consequence:** the 8 GNOME `.ogg` files **must be converted to `.wav` (44.1 kHz/16-bit PCM)** and packaged as `Resource` (or `Content` in `%ProgramFiles%`); map the names 1:1.
- **Volume:** `SoundPlayer` **has no volume property** (it plays at the system volume) → a `-20…+20 dB` slider is **infeasible** with it. Two routes:
  - (a) **recommended for v1 — without NAudio:** `System.Windows.Media.MediaPlayer` (linear `Volume 0…1`, `Open(Uri)+Play()`, plays mp3/wma/wav via WMP; keep a live reference, otherwise the GC stops the audio). Map dB→linear: `gain = 10^(dB/20)` clamped to `0…1` (0 dB = 0.5? No — 0 dB = `Volume 1.0`; -20 dB ≈ 0.1; +20 dB clamps at 1.0 with the note "gain >0 dB not supported without DSP"). [MediaPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.mediaplayer?view=windowsdesktop-9.0) · [Control-MediaElement-Volume](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-control-a-mediaelement-play-pause-stop-volume-and-speed)
  - (b) **dB-faithful — with NAudio:** **NAudio — v2.3.0 (3.0 in preview) — MIT — [GitHub](https://github.com/naudio/NAudio) · [NuGet](https://www.nuget.org/packages/NAudio/)** (`AudioFileReader.Volume 0…1`, `WaveOutEvent`/WASAPI `WasapiOut`, `VolumeSampleProvider` accepts dB via `10^(dB/20)` without creative clamping). Cost: a larger native-ish dependency + `AudioFileReader` does not decode Vorbis `.ogg` out-of-box (it would need NVorbis or the converted `.wav` files anyway).
- **Decision:** v1 = `MediaPlayer` + converted wavs + slider mapped to `0…1` with the dB label preserved in the UI (visual parity, documented partial fidelity); NAudio only if a spike shows an audible gap or if real `+dB` is requested.

## 9. Notifications/toast + wiggle

- **Learn fact (blocker for a faithful toast):** an **unpackaged** app (our v1 case, no MSIX) can send toasts, but with special steps: declare an **AUMID** (`Company.App`) + a **CLSID stub** on the Start menu shortcut (`System.AppUserModel.ID`, `System.AppUserModel.ToastActivatorCLSID`), call `RegisterAumidAndComServer(AUMID, clsid)` at startup, install via the installer before debugging, and with the stub only **protocol activation** works; **http images not supported when unpackaged** (download them to local app-data). Without this, the toast silently does not appear.
  [Toast-Desktop-Apps](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/toast-desktop-apps) · [Send-Local-Toast C#](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast) · [Migration-Guide Toolkit vs AppSDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/toast-notifications)
- **Libs:**
  - **Microsoft.Toolkit.Uwp.Notifications — v7.1.3 — MIT (.NET Foundation, header in the sources) — [NuGet](https://www.nuget.org/packages/Microsoft.Toolkit.Uwp.Notifications/) · [ToastContentBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.toolkit.uwp.notifications.toastcontentbuilder?view=win-comm-toolkit-dotnet-7.1) · [Source](https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/Microsoft.Toolkit.Uwp.Notifications/Toasts/Builder/ToastContentBuilder.cs)** — `ToastContentBuilder().AddText(…).Show()` + `ToastNotificationManagerCompat.OnActivated`; alternative modern path = `Microsoft.WindowsAppSDK` `AppNotificationManager.Register()/Show()` (requires the WinAppSDK runtime).
- **v1 decision: tray balloon (`NotifyIcon.ShowBalloonTip`), toast deferred to the MSIX milestone.** `NotifyIcon.ShowBalloonTip(timeout, title, text, icon)`: no AUMID/installer/COM, one call; timeout now **deprecated** (duration set by the OS/accessibility, typically 10–30 s imposed by the OS); one balloon at a time.
  [ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0) · [NotifyIcon-Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms)
  Map the `send-notification` pref (default false, `01 §5`) to the balloon; the Toolkit toast comes in once there is an installer with an AUMID + a local icon.
- **Wiggle (the GNOME indicator shakes 2 px/65 ms×3, `01 §7`):** no equivalent in the tray. Port = **(a)** popup scale animation on open (150 ms, parity with the dialog animation) **+ (b)** tray icon flash (alternate the `Icon` base/overlay 3×65 ms) on every new copy when `wiggle-indicator=true`. No window shake, no coupled sound (sound is a separate pref).

## 10. IPC equivalent to DBus (`Toggle/Show/Hide/ClearHistory`)

Original DBus: iface `org.gnome.Shell.Extensions.Copyous`, methods `Toggle, Show, Hide, ClearHistory(in b all)`, `ClearHistory(true)=everything, false=keeps pinned+tagged` (`01 §6`).

- **Transport: `System.IO.Pipes` named pipe.** Server in the first instance (`NamedPipeServerStream`, `PipeDirection.InOut`, `PipeTransmissionMode.Byte`, `PipeOptions.Asynchronous|CurrentUserOnly`), `WaitForConnectionAsync` loop + line-based UTF-8 `StreamReader/Writer`.
  [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0) · [NamedPipeClientStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream?view=net-10.0) · [How-to-Named-Pipes](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication)
- **Name + ACL (port of the handoff from `02`):** `Local\WindowsCM.<UserSid>` (session; `Local\` vs `Global\` in [Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0)); isolation via `PipeOptions.CurrentUserOnly` (only the same user **and** the same elevation level) instead of a custom `PipeSecurity` — `MutexSecurity`/ACL on a named mutex **does not exist on .NET Core/5+** (`02 §Dubious`). `NamedPipeServerStreamAcl.Create` with `PipeSecurity` only if an audit requires an explicit ACL.
  [PipeOptions.CurrentUserOnly](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0) · [NamedPipeServerStreamAcl.Create](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create?view=net-10.0)
- **Single-instance (from `02`):** `new Mutex(false, @"Local\WindowsCM.<sid>", out createdNew)`; if `!createdNew` → `NamedPipeClientStream(".", pipename)` `Connect(2000)` → writes the command → exits. No `MutexSecurity` on .NET 8.
- **v1 protocol — lowercase text line** (not JSON): `toggle | show | hide | clear | clear-all | ping` + `\n`; response `ok\n` / `unknown\n`. Reason: commands have no payload; line framing avoids a JSON half-read; case-insensitive; unknown = `unknown` without a crash. (JSON only if v2 needs args such as `copy <id>`.)
- **CLI:** `--toggle | --show | --hide | --clear | --clear-all` (`Environment.GetCommandLineArgs`). `--clear` = `ClearHistory(false)` (keeps pinned+tagged, = DBus `false`); `--clear-all` = `ClearHistory(true)` (everything). No separate `--clear-pinned` (the original has no pinned-only granularity; `KeepPinnedAndTagged` is atomic). A second instance with a flag forwards through the pipe and exits with code 0; no server (stale mutex) → becomes the server.
