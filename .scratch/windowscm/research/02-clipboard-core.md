# Research 02 — Núcleo clipboard Windows (WPF .NET 8)

Ticket: `.scratch/windowscm/issues/02-research-windows-clipboard-core.md` (não editado).
Método: apenas fontes primárias — Microsoft Learn (Win32/.NET), docs oficiais Tauri/Electron e código-fonte dos plugins. Cada claim abaixo cita a fonte.

## APIs confirmadas

- `AddClipboardFormatListener(HWND)` → janela recebe `WM_CLIPBOARDUPDATE` a cada mudança; registro vale até `RemoveClipboardFormatListener`. Suporte mínimo: Vista / Server 2008, API set `ext-ms-win-ntuser-misc-l1-5-1` (10.0.14393) — logo coberto em Win10 20H2+ e Win11. [AddClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener) · [WM_CLIPBOARDUPDATE](https://learn.microsoft.com/en-us/windows/win32/dataxchg/wm-clipboardupdate) · [RemoveClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-removeclipboardformatlistener) · [guia Using the Clipboard](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard) (recomenda listener em vez de clipboard viewer chain).
- `GetClipboardSequenceNumber()`: serial por window station, incrementado a cada mudança/esvaziamento; com delayed rendering, só incrementa quando renderizado. [ref](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber)
- `OpenClipboard` / `CloseClipboard` / `EmptyClipboard` / `SetClipboardData` / `GetClipboardData` / `EnumClipboardFormats` / `IsClipboardFormatAvailable` / `RegisterClipboardFormat` / `GetPriorityClipboardFormat` — todas existem com semântica abaixo. [OpenClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard) · [CloseClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-closeclipboard) · [EmptyClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-emptyclipboard) · [SetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata) · [GetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata) · [EnumClipboardFormats](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats) · [IsClipboardFormatAvailable](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-isclipboardformatavailable) · [RegisterClipboardFormat](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclipboardformata) · [operações/ownership](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)
- `DragQueryFileW(HDROP, iFile, ...)` (`iFile=0xFFFFFFFF` retorna a contagem; buffer `NULL` retorna tamanho). [ref](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew)
- `CF_HDROP` é o único formato Shell pré-definido (dispensa `RegisterClipboardFormat`); payload é `DROPFILES` + array duplo-NULL-terminated de paths absolutos. [Shell Clipboard Formats — CF_HDROP](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)
- Cut vs copy: formato registrado `"Preferred DropEffect"` (`CFSTR_PREFERREDDROPEFFECT`) com `DWORD` = `DROPEFFECT_COPY`(1) / `DROPEFFECT_MOVE`(2) (`MOVE` = origem deve remover = cut). [CFSTR_PREFERREDDROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPEFFECT constants](https://learn.microsoft.com/en-us/windows/win32/com/dropeffect-constants) · [cenários delete-on-paste](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- `CF_HTML` (`RegisterClipboardFormat("HTML Format")`): header ASCII `Version/StartHTML/EndHTML/StartFragment/EndFragment` (+ opcionais `StartSelection/EndSelection`), offsets em bytes; charset UTF-8; contexto opcional (`StartHTML=EndHTML=-1`); fragmento delimitado por `<!--StartFragment-->`/`<!--EndFragment-->`; `Version:1.0` desde Win10 20H2. [HTML Clipboard Format](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)
- Conversões sintetizadas pelo sistema: `CF_TEXT↔CF_OEMTEXT↔CF_UNICODETEXT` e `CF_BITMAP↔CF_DIB↔CF_DIBV5(+CF_PALETTE)`; `EnumClipboardFormats` enumera primeiro o formato real, depois os conversíveis; ao copiar bitmap, preferir `CF_DIB`/`CF_DIBV5` (CF_BITMAP é device-dependent/palette-relative). [Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats) · [Standard Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats) (`CF_UNICODETEXT`=13, `CF_BITMAP`=2, `CF_DIB`=8)
- `SendInput(cInputs, pInputs, cbSize)` com `INPUT`/`KEYBDINPUT` (`INPUT_KEYBOARD`=1); eventos injetados serialmente, sem intercalação; sujeito a UIPI (só injeta em integridade igual/menor; falha não sinaliza via `GetLastError`/retorno); não reseta estado do teclado (checar `GetAsyncKeyState`). [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) · [INPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-input) · [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)
- Foco: `GetForegroundWindow()` (pode retornar `NULL` durante perda de ativação); `SetForegroundWindow` restrito (processo desktop + lock timeout expirado + sem menus, E uma de: ser foreground / ter sido iniciado pelo foreground / sem foreground / último input / debug); app não pode forçar foreground — Windows pisca o botão da taskbar; `AllowSetForegroundWindow` delega o direito. [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) · [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow) · [Window Features (foreground)](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- WPF: `HwndSource.AddHook/RemoveHook` para WndProc (hooks chamados LIFO antes do processamento interno; delegate guardado por weak reference — manter referência viva). [HwndSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0)
- Message-only window: `CreateWindowEx` com `hWndParent=HWND_MESSAGE` (ou `SetParent` para converter); invisível, sem z-order, não enumerada, não recebe broadcast — só dispatch; `WM_CLIPBOARDUPDATE` é postada diretamente, então funciona. [Window Features — Message-Only Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows)
- STA: WPF usa STA; `System.Windows.Forms.Clipboard` exige thread STA (`[STAThread]` no `Main`); `System.Windows.Clipboard` segue o mesmo modelo. [STAThreadAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute?view=net-9.0) · [Forms.Clipboard (STA)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard?view=windowsdesktop-9.0) · [WPF hosting walkthrough (STA)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/walkthrough-hosting-wpf-content-in-win32?view=netframeworkdesktop-4.8) · [WPF Clipboard](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard?view=windowsdesktop-9.0)
- WPF `DataFormats`: `FileDrop` (=CF_HDROP), `Html` (="HTML Format"), `UnicodeText`, `Text`, `Bitmap`, `Dib`. [DataFormats](https://learn.microsoft.com/en-us/dotnet/api/system.windows.dataformats?view=windowsdesktop-10.0) · [SetDataObject (persistência: `copy:true` mantém após exit)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.setdataobject?view=windowsdesktop-10.0)
- PNG via `PngBitmapEncoder` (PresentationCore, base WIC). [PngBitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.pngbitmapencoder?view=windowsdesktop-10.0)
- Single-instance: `Mutex(Boolean, String, Boolean createdNew)` — padrão `createdNew` para detectar primeira instância; nome com prefixo `Local\` (sessão, default) vs `Global\` (todas as sessões Terminal Services); backslash reservado. [Mutex ctor](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex.-ctor?view=net-10.0) · [Mutex class (Global/Local)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0)
- Handoff segunda instância: `NamedPipeServerStream`/`NamedPipeClientStream` (`System.IO.Pipes`) para IPC local. [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0)
- Autostart unpackaged: chaves `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (todo logon) vs `RunOnce` (uma vez, depois deletada); sem garantia de pontualidade (sistema pode adiar). [Run and RunOnce keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)
- Autostart packaged (MSIX): extensão `windows.startupTask` no manifesto (`TaskId`, `Enabled`); vale para packaged desktop apps (Desktop Bridge, desde Win10 1607; UWP desde 1709); usuário controla em Settings/Task Manager; `RequestEnableAsync`. [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-26100) · [desktop:StartupTask schema](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask)
- Tauri `tauri-plugin-clipboard-manager` v2 expõe exatamente: `write_text`, `read_text`, `write_image`, `read_image`, `write_html`, `clear` — nada de arquivos, listener ou enumeração. [código-fonte commands.rs](https://raw.githubusercontent.com/tauri-apps/tauri-plugin-clipboard-manager/v2/src/commands.rs) · [docs do plugin (JS: readText/writeText/readImage/writeImage)](https://v2.tauri.app/reference/javascript/clipboard-manager/) · [guia](https://v2.tauri.app/plugin/clipboard/)
- Electron `clipboard`: `readText/writeText/read/write/has/clear` (modelo W3C `ClipboardItem`); formatos crus via `electron application/osclipboard;format="<nome>"` (ex. `HTML Format`); `clipboard.selection` só no Linux; sem notificação de mudança no módulo. [Electron clipboard](https://electronjs.org/docs/latest/api/clipboard) · legado `readImage/readBuffer/writeBuffer` ([source v22](https://github.com/electron/electron/blob/v22.0.3/docs/api/clipboard.md)). `globalShortcut` (registro de hotkeys globais, falha silenciosa se já capturado) é módulo separado. [globalShortcut](https://github.com/atom/electron/blob/master/docs/api/global-shortcut.md)

## APIs duvidosas / não prescritas pela doc (convenção da app, não fato do SO)

- **Polling `GetClipboardSequenceNumber` em loop**: a doc diz explicitamente o contrário — "this is not a notification method and should not be used in a polling loop; to be notified use a listener or viewer". Uso legítimo: validar cache e checagem pontual (ex. ao reativar a janela). Fallback aceitável na spec: listener primário + verificação por sequência em eventos de ativação/foco + rede de segurança em intervalo longo (minutos), não polling curto. [Using the Clipboard — sequence number](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard)
- **`Mutex` com ACL em .NET 8**: a doc .NET afirma que segurança de access-control em mutex nomeado "is only available with .NET Framework, it's not available with .NET Core or .NET 5+". Em .NET 8, não contar com `MutexSecurity`; usar escopo `Local\` + nome com SID do usuário. [Mutexes — access control](https://learn.microsoft.com/en-us/dotnet/standard/threading/mutexes)
- **Timing 250ms do original**: nenhum valor prescrito na doc. `SendInput` injeta serialmente na fila do thread foreground; o atraso necessário depende do app-alvo (processar foco + render). Spec deve tratar como constante tunável (ex. 100–300ms) com verificação de foreground, não como valor derivado do Windows.
- **Heurística de terminal** (sequência diferente para Windows Terminal/ConHost): não confirmada em fonte primária neste passe — ver Lacunas.
- **`--hidden`**: convenção de CLI da app (`Environment.GetCommandLineArgs`), sem API do SO envolvida.
- **Retry com backoff no `OpenClipboard`**: a doc só estabelece que `OpenClipboard` falha se outra janela tem o clipboard aberto e que se deve fechar após cada abertura. Retry/backoff é convenção sensata da app, não prescrita. [OpenClipboard remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard) · [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)

## Regras de ownership (leitura/escrita) — resumo normativo

1. Uma só janela abre o clipboard por vez; após cada `OpenClipboard` bem-sucedido, chamar `CloseClipboard` (libera para outras janelas). [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations) · [CloseClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-closeclipboard)
2. Escrever = `OpenClipboard` → `EmptyClipboard` (torna-se dono; dono anterior recebe `WM_DESTROYCLIPBOARD`) → N× `SetClipboardData` → `CloseClipboard`. Ordem dos formatos: do mais descritivo ao menos. [Using the Clipboard](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard) · [EnumClipboardFormats remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats)
3. `SetClipboardData` transfere ownership do handle ao sistema (`GMEM_MOVEABLE`); app não pode escrever/liberar depois (pode ler com lock até `CloseClipboard`, com unlock antes de fechar). `EmptyClipboard` com `hwnd=NULL` no open zera o dono e faz `SetClipboardData` falhar. [SetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata)
4. `GetClipboardData` retorna handle do clipboard: copiar imediatamente; nunca liberar, nunca deixar lockado, nunca usar após `CloseClipboard`/`EmptyClipboard`/novo `SetClipboardData` no mesmo formato. Antes, enumerar com `EnumClipboardFormats(0→…)` (exige clipboard aberto) ou checar `IsClipboardFormatAvailable`. [GetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata) · [EnumClipboardFormats](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats)
5. Liberação pelo sistema ao esvaziar: `DeleteObject` p/ `CF_BITMAP`, `GlobalFree` p/ `CF_DIB/CF_DIBV5/CF_TEXT/CF_UNICODETEXT`. [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)
6. Leitura de `CF_HDROP`: `OpenClipboard` → `GetClipboardData(CF_HDROP)` (= `HDROP`, i.e. `DROPFILES` em `HGLOBAL`) → `DragQueryFileW(0xFFFFFFFF)` conta → `DragQueryFileW(i)` por arquivo (1 vs N pelo count) → `CloseClipboard` sem liberar o handle. `fWide` indica Unicode vs ANSI. [CF_HDROP](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DragQueryFileW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew) · [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)
7. Cut vs copy na leitura: `RegisterClipboardFormat("Preferred DropEffect")` + `GetClipboardData` → `DWORD` com bit `DROPEFFECT_MOVE`(2)=cut, `DROPEFFECT_COPY`(1)=copy; mascarar bits em vez de comparar igualdade. [CFSTR_PREFERREDDROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/com/dropeffect-constants)
8. Ao colar arquivos de volta: reconstruir `DROPFILES` (`pFiles=offset`, `fWide=TRUE`, paths absolutos duplo-NULL) + `Preferred DropEffect` correspondente; tipos não-filesystem (`CFSTR_FILEDESCRIPTOR`) fora do escopo v1 — só detectar e marcar.
9. `CF_HTML` na prática Copyous: armazenar payload opaco e reescrever verbatim (offsets já calculados); só gerar header ao sintetizar HTML novo. [HTML Clipboard Format](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)

## Colar no app focado (fluxo normativo)

1. No momento do hotkey, capturar `GetForegroundWindow()` (alvo). [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)
2. Esconder/minimizar a própria UI e devolver foco (não confiar em `SetForegroundWindow` para o alvo: a app geralmente NÃO tem direito — só foreground/recebido-último-input/etc.; violação resulta em taskbar flashing, não em foco). [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
3. Verificar `GetForegroundWindow() == alvo`; só então `SendInput` com sequência `Ctrl↓ V↓ V↑ Ctrl↑` (ou `Shift+Insert` como alternativa — ver Lacunas p/ terminais). Checar `GetAsyncKeyState` antes (teclas presas interferem). [SendInput remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
4. UIPI: paste falha silenciosamente contra apps de integridade maior (rodar como admin). Sem sinal em retorno/`GetLastError`. [SendInput remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
5. Delay pós-foco (o "250ms"): constante tunável de UX, não valor do Windows.

## Single-instance / autostart (decisões p/ spec)

- Mutex: `Local\WindowsCM.<UserSid>` (ou GUID da app + SID), padrão `new Mutex(false, name, out createdNew)`; se `!createdNew`, encaminhar argv via `NamedPipeClientStream` ao dono e sair. Sem `MutexSecurity` em .NET 8. [Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0) · [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0)
- Autostart (app unpackaged): `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (valor = `"exe" --hidden`). `StartupTask` só com empacotamento MSIX. [Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys) · [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-26100)

## Gap Tauri/Electron (por que WPF direto)

- Tauri plugin: só texto/imagem(png)/html/write+clear (fonte: `commands.rs` + docs oficiais). Sem `CF_HDROP`, sem `Preferred DropEffect`, sem listener de mudança, sem `EnumClipboardFormats`, sem paste sintetizado. [commands.rs](https://raw.githubusercontent.com/tauri-apps/tauri-plugin-clipboard-manager/v2/src/commands.rs) · [plugin docs](https://v2.tauri.app/plugin/clipboard/)
- Electron: cobre texto/html/rtf/imagem + formatos crus via `electron application/osclipboard;format="HTML Format"` (round-trip de bytes, sem parsing `DROPFILES`/offsets `CF_HTML`), sem notificação de mudança no módulo clipboard, sem `SendInput`, sem single-instance de clipboard. [Electron clipboard](https://electronjs.org/docs/latest/api/clipboard)
- Nada dos dois cobre o trio load-bearing: `WM_CLIPBOARDUPDATE` + `CF_HDROP` com semântica cut/copy + `EnumClipboardFormats` priorizado. Daí WPF+P/Invoke direto.

## Amostra C# mínima (listener + leitura CF_HDROP)

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

// Fontes: AddClipboardFormatListener/RemoveClipboardFormatListener/WM_CLIPBOARDUPDATE
// (https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener),
// GetClipboardData/EnumClipboardFormats/OpenClipboard
// (https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata),
// DragQueryFileW (https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew),
// CFSTR_PREFERREDDROPEFFECT/DROPEFFECT (https://learn.microsoft.com/en-us/windows/win32/shell/clipboard).
internal static class NativeClipboard
{
    public const int WM_CLIPBOARDUPDATE = 0x031D; // winuser.h, ver WM_CLIPBOARDUPDATE no Learn
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_HDROP = 15;
    public const uint DROPEFFECT_COPY = 1;
    public const uint DROPEFFECT_MOVE = 2; // cut

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    // WPF: hookar com HwndSource.AddHook e tratar WM_CLIPBOARDUPDATE;
    // manter o delegate do hook vivo (weak reference interna).
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0

    public static string[] ReadFileDrop(out bool isCut)
    {
        isCut = false;
        if (!OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("OpenClipboard falhou (outro dono).");
        try
        {
            IntPtr h = GetClipboardData(CF_HDROP);
            if (h == IntPtr.Zero) return [];
            uint n = DragQueryFileW(h, 0xFFFFFFFF, null, 0);
            var files = new string[n];
            for (uint i = 0; i < n; i++)
            {
                uint len = DragQueryFileW(h, i, null, 0);
                var sb = new StringBuilder((int)len + 1);
                DragQueryFileW(h, i, sb, (uint)sb.Capacity);
                files[i] = sb.ToString();
            }
            uint pref = RegisterClipboardFormat("Preferred DropEffect");
            IntPtr p = pref != 0 ? GetClipboardData(pref) : IntPtr.Zero;
            if (p != IntPtr.Zero)
                isCut = (Marshal.ReadInt32(p) & DROPEFFECT_MOVE) != 0;
            return files; // handle é do clipboard: copiar e NÃO liberar (GetClipboardData)
        }
        finally { CloseClipboard(); }
    }
}
```

## Lacunas (não confirmado em fonte primária neste passe)

1. **Heurística de terminal**: se Windows Terminal/ConHost exigem `Shift+Insert` em vez de `Ctrl+V`, e como detectar a janela (classe `ConsoleWindowClass`, processo `WindowsTerminal.exe` via `GetWindowThreadProcessId`+nome do processo). Pastes de terminal são configuráveis pelo usuário (key bindings), então qualquer heurística precisa de spike empírico + fallback configurável. Não investigado a fundo: docs do Windows Terminal (fora do escopo Win32 Learn) e comportamento QuickEdit do ConHost.
2. **Valor exato de delay pós-foco**: sem prescrição; definir por medição (sugestão: 150–250ms default tunável + assert de `GetForegroundWindow`).
3. **Retry/backoff `OpenClipboard`**: sem prescrição de tentativas/intervalos; sugerir 5× com 10→100ms exponencial, mas validar contra apps que seguram o clipboard (Office) em spike.
4. **`CFSTR_FILEDESCRIPTOR`/`CFSTR_FILECONTENTS`** (Outlook/arquivos virtuais): escopo v1 = detectar via `EnumClipboardFormats` e marcar item como não-colável via arquivo; ronda completa fica para depois.
5. **Escrita de imagem**: confirmado `CF_DIB`-preferido + `PngBitmapEncoder` p/ persistência, mas o caminho exato `BitmapSource`→`HGLOBAL CF_DIB` via P/Invoke (vs `System.Windows.Clipboard.SetImage`) merece spike de código — não coberto por doc única.
