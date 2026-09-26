# Windows clipboard core

Type: research
Status: resolved

## Question

Define the Win32 core for WPF .NET 8: how to monitor and read/write the clipboard with parity (text, image, files), without naive polling.

Answer with APIs + formats + edge cases:
- `AddClipboardFormatListener(HWND)` → `WM_CLIPBOARDUPDATE` in a WPF window (hidden HWND), `GetClipboardSequenceNumber` as fallback
- Formats: `CF_UNICODETEXT/CF_TEXT`, `CF_BITMAP/CF_DIB`, `CF_HDROP` + `DragQueryFile` (copy vs cut, 1 vs N files), `CF_HTML` if needed
- Writing back (copy/paste) + `SendInput` to paste into the focused app (the original's 250ms timing?)
- Single-instance (named `Mutex`) + autostart `Run` key + `--hidden` args
- What the Tauri/Electron plugin does NOT cover (why direct WPF was chosen)

## Answer

Findings in `.scratch/windowscm/research/02-clipboard-core.md` (primary sources only, MS Learn/.NET + Tauri/Electron docs; 8 sections + C# P/Invoke sample + gaps).

Load-bearing for the spec:
- Listener: `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` via `HwndSource.AddHook` (delegate with a live reference) or message-only window (`HWND_MESSAGE`). `GetClipboardSequenceNumber` is NOT a notification (the docs forbid polling) — only a one-off check on activation/focus.
- `CF_HDROP` = the only predefined Shell format; `DragQueryFileW(0xFFFFFFFF)` gives the count (1→File, N→Files); cut vs copy via `"Preferred DropEffect"` (MOVE=cut). Bitmap: prefer `CF_DIB/V5`, persist PNG via `PngBitmapEncoder`. `EnumClipboardFormats` with the clipboard open (real formats before synthesized ones). `CF_HTML`: header with byte offsets + UTF-8 — v1 stores it opaque and rewrites it verbatim.
- Ownership: copy the handle immediately, never free/lock it; `SetClipboardData` transfers ownership (`GMEM_MOVEABLE`); `OpenClipboard(NULL)`+`EmptyClipboard` clears the owner. STA thread, retry with backoff.
- Paste: serial `SendInput` + UIPI (silent failure against elevated apps); flow = capture the target on the hotkey → hide the UI → assert `GetForegroundWindow` → inject `Ctrl+V`. The original's "250ms" becomes a tunable constant (no basis in the docs).
- Single-instance: `Mutex` `Local\Name+SID` + `createdNew` (ACL only exists in .NET Framework!) + named pipe handoff; unpackaged autostart = `HKCU…\Run` key (`StartupTask` requires MSIX); `--hidden` arg.
- Gap confirmed: Tauri (text/image/html/clear) and Electron (raw formats without parsing) do not cover listener + `CF_HDROP` cut/copy + prioritized enum — justifies WPF+P/Invoke.
- Gaps (spikes for the `to-tickets`/implementation phase, they do not block the spec): terminal heuristic (`Shift+Insert` vs `Ctrl+V`, detection by class/process), post-focus delay + retry policy by measurement, `CFSTR_FILEDESCRIPTOR` (virtual files) out of v1.
- Unblocks: `08` (with `03`); feeds `04` (formats→schema) and `05` (paste/incognito).
