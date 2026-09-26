# Global hotkeys + tray + popup

Type: research
Status: resolved

## Question

Define hotkeys + tray + popup placement on Windows with WPF.

Answer:
- `RegisterHotKey` / `UnregisterHotKey` → `WM_HOTKEY`: IDs, modifiers, failure if taken, why `Win+V`/`Win+Shift+V` are out, configurable defaults `Ctrl+Shift+V` (open) + `Ctrl+Shift+Alt+V` (incognito)
- Tray: `NotifyIcon`/`H.NotifyIcon` (WPF), tooltip, click opens, menu `Open/Incognito/Clear(keep pins+tags)/Settings/Exit`, icon + single-click vs double behavior
- Popup: `ShowActivated=false` window, place at `GetCursorPos` (mouse) vs the text caret (caret API), multi-monitor/DPI, focus/focus loss (auto-hide?), does `Esc` close?
- Map Copyous's internal shortcuts (Enter/Space copy/paste, Ctrl+Enter default action, Ctrl+S pin, Delete, Ctrl+0..9 jump, Alt pinned, Ctrl+Tab type, Ctrl+` tag) to WPF

## Answer

Findings in `.scratch/windowscm/research/03-hotkeys-tray-popup.md` (primary sources + official repos only; 6 sections + C#/XAML samples + gaps).

Load-bearing for the spec:
- `RegisterHotKey(hWnd,id,mods,vk)` per HWND, app ids `0x0000–0xBFFF`, `WM_HOTKEY=0x0312`; re-registering requires an explicit `UnregisterHotKey`. `MOD_WIN` reserved + the OS's `Win+V` → out (confirms Q4). `MOD_NOREPEAT` always (Win7+). Failure = zero return + `GetLastError` (`SetLastError=true`); hooks via `HwndSource.AddHook`.
- Defaults: `Ctrl+Shift+V` collides (paste-without-formatting in Windows/VS Code) but stays configurable; `Ctrl+Shift+Alt+V` low risk. Persist in user-scoped `Properties.Settings` + `Save()`; runtime change = Unregister+Register.
- Tray: **`H.NotifyIcon.WPF` (`TaskbarIcon`, MIT)** over WinForms `NotifyIcon` (commands, binding). Left = toggle popup, right = menu `Open/Incognito/Clear(keep pins+tags)/Settings/Exit`; double-click only an alias.
- Popup: `ShowActivated=False` + `Topmost`, `Deactivated→Hide()`, `Esc` closes, ~150ms animation via Storyboard. **v1 position = `GetCursorPos` → DIPs (`TransformFromDevice`/`GetDpi`) + clamp to `WorkingArea`**; global caret (`GetGUIThreadInfo`+`ClientToScreen`, UIA `GetCaretRange`) is left for v2. Per-monitor DPI via the manifest.
- **Parity deviation: `Alt` alone becomes `Key.System`/menu mode in WPF → toggle-pinned becomes `Alt+P`** (contradicts Copyous, reopened by the OS; record it in the spec). `Enter/Space/Delete/Ctrl+A` in a scoped `PreviewKeyDown` (outside the search), the rest in `Window.InputBindings`.
- Gaps (implementation): `ERROR_HOTKEY_ALREADY_REGISTERED` (hypothesized 1409, confirm against `winerror.h`), `H.NotifyIcon.Wpf` version/TFMs on NuGet, UIA `GetCaretRange`, exact `ApplicationCommands` gestures, `szTip` tooltip/Win11 balloon limit.
- Unblocks: `07` (with `01`) and `08` (with `02`).
