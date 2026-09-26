# Research 02 — Windows clipboard core (WPF .NET 8)

Ticket: `.scratch/windowscm/issues/02-research-windows-clipboard-core.md` (not edited).
Method: primary sources only — Microsoft Learn (Win32/.NET), official Tauri/Electron docs and the plugins' source code. Every claim below cites its source.

## Confirmed APIs

- `AddClipboardFormatListener(HWND)` → the window receives `WM_CLIPBOARDUPDATE` on every change; the registration holds until `RemoveClipboardFormatListener`. Minimum support: Vista / Server 2008, API set `ext-ms-win-ntuser-misc-l1-5-1` (10.0.14393) — so it is covered on Win10 20H2+ and Win11. [AddClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener) · [WM_CLIPBOARDUPDATE](https://learn.microsoft.com/en-us/windows/win32/dataxchg/wm-clipboardupdate) · [RemoveClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-removeclipboardformatlistener) · [Using the Clipboard guide](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard) (recommends a listener instead of the clipboard viewer chain).
- `GetClipboardSequenceNumber()`: serial per window station, incremented on every change/emptying; with delayed rendering, it only increments when rendered. [ref](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber)
- `OpenClipboard` / `CloseClipboard` / `EmptyClipboard` / `SetClipboardData` / `GetClipboardData` / `EnumClipboardFormats` / `IsClipboardFormatAvailable` / `RegisterClipboardFormat` / `GetPriorityClipboardFormat` — all exist, with the semantics below. [OpenClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard) · [CloseClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-closeclipboard) · [EmptyClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-emptyclipboard) · [SetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata) · [GetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata) · [EnumClipboardFormats](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats) · [IsClipboardFormatAvailable](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-isclipboardformatavailable) · [RegisterClipboardFormat](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclipboardformata) · [operations/ownership](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)
- `DragQueryFileW(HDROP, iFile, ...)` (`iFile=0xFFFFFFFF` returns the count; a `NULL` buffer returns the size). [ref](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew)
- `CF_HDROP` is the only predefined Shell format (no `RegisterClipboardFormat` needed); the payload is `DROPFILES` + a double-NULL-terminated array of absolute paths. [Shell Clipboard Formats — CF_HDROP](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)
- Cut vs copy: registered format `"Preferred DropEffect"` (`CFSTR_PREFERREDDROPEFFECT`) with `DWORD` = `DROPEFFECT_COPY`(1) / `DROPEFFECT_MOVE`(2) (`MOVE` = the source must remove = cut). [CFSTR_PREFERREDDROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPEFFECT constants](https://learn.microsoft.com/en-us/windows/win32/com/dropeffect-constants) · [delete-on-paste scenarios](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- `CF_HTML` (`RegisterClipboardFormat("HTML Format")`): ASCII header `Version/StartHTML/EndHTML/StartFragment/EndFragment` (+ optional `StartSelection/EndSelection`), offsets in bytes; UTF-8 charset; optional context (`StartHTML=EndHTML=-1`); fragment delimited by `<!--StartFragment-->`/`<!--EndFragment-->`; `Version:1.0` since Win10 20H2. [HTML Clipboard Format](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)
- Conversions synthesized by the system: `CF_TEXT↔CF_OEMTEXT↔CF_UNICODETEXT` and `CF_BITMAP↔CF_DIB↔CF_DIBV5(+CF_PALETTE)`; `EnumClipboardFormats` enumerates the real format first, then the convertible ones; when copying a bitmap, prefer `CF_DIB`/`CF_DIBV5` (CF_BITMAP is device-dependent/palette-relative). [Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats) · [Standard Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats) (`CF_UNICODETEXT`=13, `CF_BITMAP`=2, `CF_DIB`=8)
- `SendInput(cInputs, pInputs, cbSize)` with `INPUT`/`KEYBDINPUT` (`INPUT_KEYBOARD`=1); events are injected serially, without interleaving; subject to UIPI (only injects into equal/lower integrity; a failure is not signaled via `GetLastError`/the return value); does not reset the keyboard state (check `GetAsyncKeyState`). [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) · [INPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-input) · [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)
- Focus: `GetForegroundWindow()` (can return `NULL` while activation is being lost); `SetForegroundWindow` is restricted (desktop process + lock timeout expired + no menus, AND one of: being the foreground / having been started by the foreground / no foreground / last input / debug); an app cannot force the foreground — Windows flashes the taskbar button; `AllowSetForegroundWindow` delegates the right. [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) · [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow) · [Window Features (foreground)](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- WPF: `HwndSource.AddHook/RemoveHook` for WndProc (hooks called LIFO before internal processing; the delegate is held by a weak reference — keep a live reference). [HwndSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0)
- Message-only window: `CreateWindowEx` with `hWndParent=HWND_MESSAGE` (or `SetParent` to convert); invisible, no z-order, not enumerated, does not receive broadcasts — dispatch only; `WM_CLIPBOARDUPDATE` is posted directly, so it works. [Window Features — Message-Only Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows)
- STA: WPF uses STA; `System.Windows.Forms.Clipboard` requires an STA thread (`[STAThread]` on `Main`); `System.Windows.Clipboard` follows the same model. [STAThreadAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute?view=net-9.0) · [Forms.Clipboard (STA)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard?view=windowsdesktop-9.0) · [WPF hosting walkthrough (STA)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/walkthrough-hosting-wpf-content-in-win32?view=netframeworkdesktop-4.8) · [WPF Clipboard](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard?view=windowsdesktop-9.0)
- WPF `DataFormats`: `FileDrop` (=CF_HDROP), `Html` (="HTML Format"), `UnicodeText`, `Text`, `Bitmap`, `Dib`. [DataFormats](https://learn.microsoft.com/en-us/dotnet/api/system.windows.dataformats?view=windowsdesktop-10.0) · [SetDataObject (persistence: `copy:true` keeps it after exit)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.setdataobject?view=windowsdesktop-10.0)
- PNG via `PngBitmapEncoder` (PresentationCore, WIC-based). [PngBitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.pngbitmapencoder?view=windowsdesktop-10.0)
- Single-instance: `Mutex(Boolean, String, Boolean createdNew)` — the `createdNew` pattern to detect the first instance; name with the `Local\` prefix (session, default) vs `Global\` (all Terminal Services sessions); backslash reserved. [Mutex ctor](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex.-ctor?view=net-10.0) · [Mutex class (Global/Local)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0)
- Second-instance handoff: `NamedPipeServerStream`/`NamedPipeClientStream` (`System.IO.Pipes`) for local IPC. [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0)
- Unpackaged autostart: keys `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (every logon) vs `RunOnce` (once, then deleted); no timeliness guarantee (the system may defer it). [Run and RunOnce keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)
- Packaged autostart (MSIX): `windows.startupTask` extension in the manifest (`TaskId`, `Enabled`); applies to packaged desktop apps (Desktop Bridge, since Win10 1607; UWP since 1709); the user controls it in Settings/Task Manager; `RequestEnableAsync`. [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-26100) · [desktop:StartupTask schema](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask)
- Tauri `tauri-plugin-clipboard-manager` v2 exposes exactly: `write_text`, `read_text`, `write_image`, `read_image`, `write_html`, `clear` — no files, listener or enumeration. [commands.rs source code](https://raw.githubusercontent.com/tauri-apps/tauri-plugin-clipboard-manager/v2/src/commands.rs) · [plugin docs (JS: readText/writeText/readImage/writeImage)](https://v2.tauri.app/reference/javascript/clipboard-manager/) · [guide](https://v2.tauri.app/plugin/clipboard/)
- Electron `clipboard`: `readText/writeText/read/write/has/clear` (W3C `ClipboardItem` model); raw formats via `electron application/osclipboard;format="<name>"` (e.g. `HTML Format`); `clipboard.selection` on Linux only; no change notification in the module. [Electron clipboard](https://electronjs.org/docs/latest/api/clipboard) · legacy `readImage/readBuffer/writeBuffer` ([source v22](https://github.com/electron/electron/blob/v22.0.3/docs/api/clipboard.md)). `globalShortcut` (global hotkey registration, fails silently if already captured) is a separate module. [globalShortcut](https://github.com/atom/electron/blob/master/docs/api/global-shortcut.md)

## Dubious APIs / not prescribed by the docs (app convention, not an OS fact)

- **Polling `GetClipboardSequenceNumber` in a loop**: the docs explicitly say the opposite — "this is not a notification method and should not be used in a polling loop; to be notified use a listener or viewer". Legitimate use: cache validation and one-off checks (e.g. when the window is reactivated). Acceptable fallback in the spec: primary listener + a sequence check on activation/focus events + a long-interval safety net (minutes), not short polling. [Using the Clipboard — sequence number](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard)
- **`Mutex` with ACL on .NET 8**: the .NET docs state that access-control security on a named mutex "is only available with .NET Framework, it's not available with .NET Core or .NET 5+". On .NET 8, do not count on `MutexSecurity`; use the `Local\` scope + a name with the user's SID. [Mutexes — access control](https://learn.microsoft.com/en-us/dotnet/standard/threading/mutexes)
- **The original's 250ms timing**: no value prescribed in the docs. `SendInput` injects serially into the foreground thread's queue; the required delay depends on the target app (processing focus + render). The spec should treat it as a tunable constant (e.g. 100–300ms) with a foreground check, not as a value derived from Windows.
- **Terminal heuristic** (a different sequence for Windows Terminal/ConHost): not confirmed in a primary source in this pass — see Gaps.
- **`--hidden`**: an app CLI convention (`Environment.GetCommandLineArgs`), no OS API involved.
- **Retry with backoff on `OpenClipboard`**: the docs only establish that `OpenClipboard` fails if another window has the clipboard open and that it must be closed after each open. Retry/backoff is a sensible app convention, not prescribed. [OpenClipboard remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard) · [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)

## Ownership rules (read/write) — normative summary

1. Only one window opens the clipboard at a time; after each successful `OpenClipboard`, call `CloseClipboard` (releases it to other windows). [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations) · [CloseClipboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-closeclipboard)
2. Write = `OpenClipboard` → `EmptyClipboard` (becomes the owner; the previous owner receives `WM_DESTROYCLIPBOARD`) → N× `SetClipboardData` → `CloseClipboard`. Format order: from the most descriptive to the least. [Using the Clipboard](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard) · [EnumClipboardFormats remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats)
3. `SetClipboardData` transfers ownership of the handle to the system (`GMEM_MOVEABLE`); the app cannot write/free it afterwards (it can read it with a lock until `CloseClipboard`, unlocking before closing). `EmptyClipboard` with `hwnd=NULL` at open clears the owner and makes `SetClipboardData` fail. [SetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata)
4. `GetClipboardData` returns a clipboard handle: copy immediately; never free it, never leave it locked, never use it after `CloseClipboard`/`EmptyClipboard`/a new `SetClipboardData` in the same format. Beforehand, enumerate with `EnumClipboardFormats(0→…)` (requires an open clipboard) or check `IsClipboardFormatAvailable`. [GetClipboardData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata) · [EnumClipboardFormats](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumclipboardformats)
5. Freeing by the system on emptying: `DeleteObject` for `CF_BITMAP`, `GlobalFree` for `CF_DIB/CF_DIBV5/CF_TEXT/CF_UNICODETEXT`. [Clipboard Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)
6. Reading `CF_HDROP`: `OpenClipboard` → `GetClipboardData(CF_HDROP)` (= `HDROP`, i.e. `DROPFILES` in an `HGLOBAL`) → `DragQueryFileW(0xFFFFFFFF)` counts → `DragQueryFileW(i)` per file (1 vs N by the count) → `CloseClipboard` without freeing the handle. `fWide` indicates Unicode vs ANSI. [CF_HDROP](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DragQueryFileW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew) · [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)
7. Cut vs copy on read: `RegisterClipboardFormat("Preferred DropEffect")` + `GetClipboardData` → `DWORD` with bit `DROPEFFECT_MOVE`(2)=cut, `DROPEFFECT_COPY`(1)=copy; mask the bits instead of comparing for equality. [CFSTR_PREFERREDDROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) · [DROPEFFECT](https://learn.microsoft.com/en-us/windows/win32/com/dropeffect-constants)
8. When pasting files back: rebuild `DROPFILES` (`pFiles=offset`, `fWide=TRUE`, double-NULL absolute paths) + the matching `Preferred DropEffect`; non-filesystem types (`CFSTR_FILEDESCRIPTOR`) are out of v1 scope — only detect and flag them.
9. `CF_HTML` in Copyous practice: store an opaque payload and rewrite it verbatim (offsets already computed); only generate a header when synthesizing new HTML. [HTML Clipboard Format](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)

## Pasting into the focused app (normative flow)

1. At hotkey time, capture `GetForegroundWindow()` (target). [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)
2. Hide/minimize our own UI and hand focus back (do not rely on `SetForegroundWindow` for the target: the app usually does NOT have the right — only foreground/received-last-input/etc.; a violation results in taskbar flashing, not in focus). [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
3. Verify `GetForegroundWindow() == target`; only then `SendInput` with the sequence `Ctrl↓ V↓ V↑ Ctrl↑` (or `Shift+Insert` as an alternative — see Gaps for terminals). Check `GetAsyncKeyState` first (stuck keys interfere). [SendInput remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
4. UIPI: paste fails silently against higher-integrity apps (running as admin). No signal in the return value/`GetLastError`. [SendInput remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
5. Post-focus delay (the "250ms"): a tunable UX constant, not a Windows value.

## Single-instance / autostart (decisions for the spec)

- Mutex: `Local\WindowsCM.<UserSid>` (or the app GUID + SID), pattern `new Mutex(false, name, out createdNew)`; if `!createdNew`, forward argv via `NamedPipeClientStream` to the owner and exit. No `MutexSecurity` on .NET 8. [Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-9.0) · [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0)
- Autostart (unpackaged app): `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (value = `"exe" --hidden`). `StartupTask` only with MSIX packaging. [Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys) · [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-26100)

## Tauri/Electron gap (why WPF directly)

- Tauri plugin: only text/image(png)/html/write+clear (source: `commands.rs` + official docs). No `CF_HDROP`, no `Preferred DropEffect`, no change listener, no `EnumClipboardFormats`, no synthesized paste. [commands.rs](https://raw.githubusercontent.com/tauri-apps/tauri-plugin-clipboard-manager/v2/src/commands.rs) · [plugin docs](https://v2.tauri.app/plugin/clipboard/)
- Electron: covers text/html/rtf/image + raw formats via `electron application/osclipboard;format="HTML Format"` (byte round-trip, no `DROPFILES` parsing/`CF_HTML` offsets), no change notification in the clipboard module, no `SendInput`, no clipboard single-instance. [Electron clipboard](https://electronjs.org/docs/latest/api/clipboard)
- Neither of the two covers the load-bearing trio: `WM_CLIPBOARDUPDATE` + `CF_HDROP` with cut/copy semantics + prioritized `EnumClipboardFormats`. Hence WPF+P/Invoke directly.

## Minimal C# sample (listener + CF_HDROP read)

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

// Sources: AddClipboardFormatListener/RemoveClipboardFormatListener/WM_CLIPBOARDUPDATE
// (https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener),
// GetClipboardData/EnumClipboardFormats/OpenClipboard
// (https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboarddata),
// DragQueryFileW (https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew),
// CFSTR_PREFERREDDROPEFFECT/DROPEFFECT (https://learn.microsoft.com/en-us/windows/win32/shell/clipboard).
internal static class NativeClipboard
{
    public const int WM_CLIPBOARDUPDATE = 0x031D; // winuser.h, see WM_CLIPBOARDUPDATE on Learn
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

    // WPF: hook with HwndSource.AddHook and handle WM_CLIPBOARDUPDATE;
    // keep the hook delegate alive (internal weak reference).
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0

    public static string[] ReadFileDrop(out bool isCut)
    {
        isCut = false;
        if (!OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("OpenClipboard failed (another owner).");
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
            return files; // the handle belongs to the clipboard: copy it and do NOT free it (GetClipboardData)
        }
        finally { CloseClipboard(); }
    }
}
```

## Gaps (not confirmed in a primary source in this pass)

1. **Terminal heuristic**: whether Windows Terminal/ConHost require `Shift+Insert` instead of `Ctrl+V`, and how to detect the window (class `ConsoleWindowClass`, process `WindowsTerminal.exe` via `GetWindowThreadProcessId`+process name). Terminal pastes are user-configurable (key bindings), so any heuristic needs an empirical spike + a configurable fallback. Not investigated in depth: the Windows Terminal docs (outside the Win32 Learn scope) and ConHost's QuickEdit behavior.
2. **Exact post-focus delay value**: no prescription; define it by measurement (suggestion: a tunable 150–250ms default + a `GetForegroundWindow` assert).
3. **Retry/backoff for `OpenClipboard`**: no prescribed attempts/intervals; suggest 5× with exponential 10→100ms, but validate against apps that hold the clipboard (Office) in a spike.
4. **`CFSTR_FILEDESCRIPTOR`/`CFSTR_FILECONTENTS`** (Outlook/virtual files): v1 scope = detect via `EnumClipboardFormats` and flag the item as not pasteable as a file; the full round is left for later.
5. **Image writing**: confirmed `CF_DIB`-preferred + `PngBitmapEncoder` for persistence, but the exact `BitmapSource`→`HGLOBAL CF_DIB` path via P/Invoke (vs `System.Windows.Clipboard.SetImage`) deserves a code spike — not covered by a single doc.
