# 03 — Global hotkeys + tray + popup (primary sources)

Ticket: `.scratch/windowscm/issues/03-research-hotkeys-tray-popup.md` (not edited).
Rule: primary sources only — Microsoft Learn (Win32 / WPF / .NET),
`dotnet/wpf`, `microsoft/WPF-Samples`, the official repos `hardcodet/wpf-notifyicon`
and `HavenDV/H.NotifyIcon`, official VS Code docs (§2 only, conflicting defaults).
Each claim ends with the primary link in parentheses.

## 1. `RegisterHotKey` / `UnregisterHotKey` → `WM_HOTKEY`

### 1.1 Signature

```c
BOOL RegisterHotKey(HWND hWnd, int id, UINT fsModifiers, UINT vk);
BOOL UnregisterHotKey(HWND hWnd, int id);
```

- `hWnd`: window that receives `WM_HOTKEY`. If `NULL`, the message goes to the
  calling thread's queue and must be handled in the message loop.
  ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- `UnregisterHotKey(hWnd, id)` frees a hotkey registered by the calling thread;
  `hWnd` must be `NULL` if the hotkey is not associated with a window.
  ([UnregisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey))
- Associating a hotkey with a window created by **another thread** fails; in that
  case (and if the combination is already registered) the return value is zero
  and the details come from `GetLastError`.
  ([RegisterHotKey — Return value](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.2 Per-window IDs (safe range)

- An application uses an `id` in `0x0000–0xBFFF`; a shared DLL uses
  `0xC000–0xFFFF` (the `GlobalAddAtom` range); a DLL should obtain the id via
  `GlobalAddAtom` so it does not collide with other DLLs.
  ([RegisterHotKey — Windows CE 3.0, archived, same semantics](https://learn.microsoft.com/en-us/previous-versions/ms961355(v=msdn.10)))
- WindowsCM is an app (not a DLL): use small constants per `HWND`
  (e.g. `1` = open, `2` = incognito). The `id` only needs to be unique **within
  the (`hWnd`, thread) pair** — it is what arrives in the `wParam` of `WM_HOTKEY`.
  ([WM_HOTKEY — wParam](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))
- Careful when re-registering: if a hotkey with the same `hWnd`+`id` already
  exists, the old one is **kept alongside** the new one — the app must call
  `UnregisterHotKey` explicitly on the old one.
  ([RegisterHotKey — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.3 Modifiers

| Flag | Value | Meaning |
|---|---|---|
| `MOD_ALT` | `0x0001` | Alt held down |
| `MOD_CONTROL` | `0x0002` | Ctrl held down |
| `MOD_SHIFT` | `0x0004` | Shift held down |
| `MOD_WIN` | `0x0008` | Windows key — **reserved for the OS** (see §1.5) |
| `MOD_NOREPEAT` | `0x4000` | Keyboard auto-repeat does not generate multiple notifications |

Table and the `MOD_WIN` reservation in
([RegisterHotKey — fsModifiers](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).
`vk` is any virtual-key code
([Virtual Key Codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)).

### 1.4 `MOD_NOREPEAT` — since which Windows version

- The flag exists with the caveat **"Windows Vista: This flag is not supported"**,
  i.e., in practice use it starting with **Windows 7+**.
  ([RegisterHotKey — fsModifiers](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- Official example: `ALT+b` with `MOD_ALT | MOD_NOREPEAT` registered for the
  thread (`hWnd = NULL`) and read via `GetMessage`/`WM_HOTKEY` — same page and in
  the sample
  ([Windows-classic-samples RegisterHotKey.cpp](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/winui/RegisterHotKey/RegisterHotKey.cpp)).
- Recommendation: always combine `MOD_NOREPEAT` into WindowsCM's hotkeys
  (opening the popup while holding the keys down must not fire N times).

### 1.5 `ERROR_HOTKEY_ALREADY_REGISTERED` error — detection and fallback

- The docs only promise: it returns zero; "Typically, `RegisterHotKey` also
  fails if the keystrokes specified for the hot key have already been
  registered for another hot key"; details via `GetLastError`.
  ([RegisterHotKey — Return value](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- Detection pattern in C# (symbolic name `ERROR_HOTKEY_ALREADY_REGISTERED`;
  **confirm the numeric value in `winerror.h` / "System Error Codes" at
  implementation time** — gap §6.1):

```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

if (!RegisterHotKey(hwnd, id, mods, vk))
{
    int err = Marshal.GetLastWin32Error(); // requires SetLastError = true
    if (err == 1409 /* ERROR_HOTKEY_ALREADY_REGISTERED — confirm in winerror.h */)
    {
        // Fallback: warn in a tray balloon + open the hotkeys screen asking for another combination.
        // Never try UnregisterHotKey on someone else's hotkey: only the owning thread can free it.
    }
}
```

- Only the thread that registered it can free it via `UnregisterHotKey`
  (an official discussion confirms the per-thread semantics).
  ([MS Q&A — RegisterHotKey/UnregisterHotKey per thread](https://learn.microsoft.com/en-us/answers/questions/1343773/does-the-shortcut-key-registered-by-the-registerho))

### 1.6 Why `Win+V` / `Win+Shift+V` are ruled out

1. **Generic reservation**: "Keyboard shortcuts that involve the WINDOWS key are
   reserved for use by the operating system" and "Hotkeys that involve the
   Windows key are reserved for use by the operating system".
   ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
   [WM_HOTKEY](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))
2. **Specific conflict**: `Win+V` opens the Windows clipboard history
   ("Press Windows logo key + V").
   ([Clipboard History — Microsoft Windows](https://www.microsoft.com/en-gb/windows/tips/clipboard-history),
   [Using the clipboard — Microsoft Support](https://support.microsoft.com/en-us/windows/apps/using-the-clipboard))
3. `Win+Shift+V` falls under the same reservation (any combo with `MOD_WIN`), so
   `RegisterHotKey(MOD_WIN|MOD_SHIFT, 'V')` tends to fail or to fight with the
   OS — same basis as items 1–2. An official answer to an analogous case
   (`Win+D`) recommends not using the Windows key as a hotkey.
   ([MS Q&A — hotkey with Windows key](https://learn.microsoft.com/en-us/answers/questions/1020405/error-while-registering-the-hotkey-in-c))
4. Extra: `F12` is reserved for the debugger and should not be registered as a hotkey.
   ([RegisterHotKey — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.7 Receiving via `HwndSource.AddHook` in WPF (minimal sample)

- `HwndSource.AddHook(HwndSourceHook)` receives **all** of the window's messages;
  it is the path for messages with no WPF equivalent (such as `WM_HOTKEY = 0x0312`).
  ([HwndSource.AddHook](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource.addhook))
- `HwndSource` wraps WPF content in an `HWND`; `Handle` exposes the `HWND`
  for P/Invoke; a hook can be added at construction or later via
  `AddHook`.
  ([HwndSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0),
  [WPF and Win32 interop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation),
  [dotnet/wpf source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndSource.cs))

```csharp
// cs: HotkeyService.cs (minimal, main window already created)
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312; // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
    const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_ALT = 0x0001, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int IdOpen = 1, IdIncognito = 2;
    HwndSource? _src;

    public void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(window);
            _src = HwndSource.FromHwnd(helper.Handle);
            _src?.AddHook(WndProc);
            RegisterHotKey(helper.Handle, IdOpen, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.V));
            RegisterHotKey(helper.Handle, IdIncognito, MOD_CONTROL | MOD_SHIFT | MOD_ALT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.V));
        };
        window.Closed += (_, _) => Dispose();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            handled = true;
            if (wParam.ToInt32() == IdOpen) /* open popup */;
            if (wParam.ToInt32() == IdIncognito) /* open incognito */;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_src is null) return;
        var hwnd = _src.Handle;
        UnregisterHotKey(hwnd, IdOpen);
        UnregisterHotKey(hwnd, IdIncognito);
        _src.RemoveHook(WndProc);
        _src = null;
    }
}
```

- `wParam` = hotkey id; `lParam`: low word = modifiers, high word =
  virtual-key code.
  ([WM_HOTKEY — Parameters](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))

## 2. Defaults `Ctrl+Shift+V` (open) and `Ctrl+Shift+Alt+V` (incognito)

### 2.1 Risk of conflict with common apps

- **Real, documented conflict**: `Ctrl+Shift+V` = "paste as plain text"
  (paste without formatting) in the official list of Windows shortcuts.
  ([Windows shortcuts — Microsoft](https://www.microsoft.com/en-us/windows/tips/windows-shortcuts))
- **VS Code**: `Ctrl+Shift+V` is contested — it pastes in the integrated terminal
  **and** opens the Markdown preview (`markdown.showPreview` when
  `editorLangId == 'markdown'`); an official issue shows the clash and a
  workaround via `keybindings.json`.
  ([VS Code — Default keybindings](https://code.visualstudio.com/docs/reference/default-keybindings),
  [microsoft/vscode#315171](https://github.com/microsoft/vscode/issues/315171))
-Nature of the clash: a **global** hotkey (`RegisterHotKey`) fires even with
  another app in focus, so with WindowsCM running, `Ctrl+Shift+V` opens the popup
  instead of "paste as plain text" in the focused app. For a clipboard manager it
  is acceptable as a default (the user wants the popup under the cursor), but it
  **must** be configurable + show the conflict warning — hence §2.2.
- `Ctrl+Shift+Alt+V` (incognito): a four-key combination, with no known default
  in the apps researched; low risk, but the same configurability rule
  (any app may have registered it first → §1.5).

### 2.2 Configurability (persist + re-register at runtime)

- Persist as **user-scoped settings**: `Properties.Settings.Default.<name>`,
  read via `Properties.Settings.Default`, write + `Save()` so it lasts
  across sessions (`user.config` created on demand; defaults live in
  `app.exe.config`).
  ([Using Application Settings and User Settings](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/using-application-settings-and-user-settings),
  [How To: Write User Settings at Run Time with C#](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-write-user-settings-at-run-time-with-csharp),
  [How To: Read Settings at Run Time With C#](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-read-settings-at-run-time-with-csharp),
  [Application Settings Architecture](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/application-settings-architecture))
- Re-registering at runtime = `UnregisterHotKey(hWnd, oldId)` **before**
  `RegisterHotKey(hWnd, newId, ...)` (the docs require freeing the old one
  explicitly — §1.2). Flow: failed → check `ERROR_HOTKEY_ALREADY_REGISTERED`
  (§1.5) → toast/balloon + reopen the hotkey editor.

```csharp
// cs: hotkey change at runtime (same HWND/id — explicit Unregister, cf. §1.2)
UnregisterHotKey(hwnd, IdOpen);
if (!RegisterHotKey(hwnd, IdOpen, newMods | MOD_NOREPEAT, newVk))
{
    int err = Marshal.GetLastWin32Error();
    // err == ERROR_HOTKEY_ALREADY_REGISTERED → revert to default and notify
}
Properties.Settings.Default.HotkeyOpen = gestureString; // e.g. "Ctrl+Shift+V"
Properties.Settings.Default.Save();
```

## 3. Tray in WPF

WPF has no `NotifyIcon` of its own — the two primary options:

### 3.1 Option A — `System.Windows.Forms.NotifyIcon`

- WinForms component for the icon of a background process in the notification
  area; key props `Icon` + `Visible` (the icon only appears with
  `Visible = true`).
  ([NotifyIcon Component Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms),
  [NotifyIcon Class](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0))
- `Icon` is a `System.Drawing.Icon` loaded from an `.ico`; `Text` = tooltip on
  hover; the official example uses `DoubleClick` to activate the form and a
  `ContextMenu` with an Exit item.
  ([Add Icons to the TaskBar](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/app-icons-to-the-taskbar-with-wf-notifyicon),
  [NotifyIcon.Text](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.text?view=windowsdesktop-10.0))
- Balloon: `ShowBalloonTip(timeout, title, text, icon)` + props
  `BalloonTipText/Title/Icon`; if a balloon is already visible, the timeout is
  ignored (behavior varies by OS/app).
  ([NotifyIcon.ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0))
- Pros: zero external dependencies, stable .NET API, enough for icon +
  tooltip + menu + balloon. Cons (engineering assessment, API facts on the
  pages above): it is WinForms (`ContextMenuStrip`, no native WPF data binding,
  no WPF `ICommand`, plain-text tooltip with no rich XAML tooltip);
  requires `Dispose()`/`Visible=false` on exit, otherwise the ghost icon stays
  until it is hovered; the WPF project must reference WinForms.

```csharp
// cs: minimal WinForms wiring inside the WPF App
_notifyIcon = new System.Windows.Forms.NotifyIcon
{
    Icon = new System.Drawing.Icon("Assets/app.ico"),
    Text = "WindowsCM",
    Visible = true,
    ContextMenuStrip = menu, // Open / Incognito / Clear (keep pins+tags) / Settings / Exit
};
_notifyIcon.MouseClick += (_, e) =>
{
    if (e.Button == System.Windows.Forms.MouseButtons.Left) TogglePopup();
    // right button: ContextMenuStrip opens by itself
};
_notifyIcon.DoubleClick += (_, _) => TogglePopup(); // the official sample uses DoubleClick to activate
```

### 3.2 Option B — `H.NotifyIcon.WPF` (`TaskbarIcon`) — recommended

- Pure WPF control (does not wrap WinForms): rich tooltips, popups,
  context menus, balloons and **command support on single/double-click**;
  `MenuActivation`/`PopupActivation` configure which click opens the menu vs the popup.
  ([hardcodet/wpf-notifyicon — README](https://github.com/hardcodet/wpf-notifyicon),
  [H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md))
- `H.NotifyIcon` is the active continuation of the inactive base project, for
  .NET 6+ WPF/WinUI/Uno/Console; packages `H.NotifyIcon.Wpf` etc.
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md))
- **MIT license** (confirmed in the file).
  ([H.NotifyIcon — LICENSE.md](https://github.com/HavenDV/H.NotifyIcon/blob/master/LICENSE.md))
- Canonical XAML sample (the same in both repos):

```xml
<!-- XAML: TaskbarIcon with menu + commands (adapt for WindowsCM) -->
<Window xmlns:tb="clr-namespace:H.NotifyIcon;assembly=H.NotifyIcon.Wpf" ...>
  <tb:TaskbarIcon x:Name="Tray"
                  ToolTipText="WindowsCM"
                  IconSource="/Assets/app.ico"
                  ContextMenu="{StaticResource TrayMenu}"
                  MenuActivation="RightClick"
                  LeftClickCommand="{Binding TogglePopupCommand}"
                  DoubleClickCommand="{Binding TogglePopupCommand}" />
</Window>
```

```xml
<!-- XAML: menu Open/Incognito/Clear(keep pins+tags)/Settings/Exit -->
<ContextMenu x:Key="TrayMenu">
  <MenuItem Header="Open" Command="{Binding OpenCommand}" />
  <MenuItem Header="Open incognito" Command="{Binding OpenIncognitoCommand}" />
  <MenuItem Header="Clear (keeps pins and tags)" Command="{Binding ClearUnpinnedCommand}" />
  <Separator />
  <MenuItem Header="Settings" Command="{Binding OpenSettingsCommand}" />
  <MenuItem Header="Exit" Command="{Binding ExitCommand}" />
</ContextMenu>
```

- Useful extras already in the repo: `TrayPopup`/`TrayToolTip` (rich XAML
  popup/tooltip with data binding), `GeneratedIconSource` (dynamic icon, e.g. a
  counter — useful for a badge), `ForceCreate()` (creates the icon even when
  windowless) and Efficiency Mode, automatic re-creation if Explorer restarts
  (`TaskbarCreated`).
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md),
  [TaskbarIcon.cs](https://github.com/HavenDV/H.NotifyIcon/blob/master/src/libs/H.NotifyIcon.Shared/TaskbarIcon.cs))
- Decision: **Option B**. Reason: rich popup + `ICommand` + binding to
  WindowsCM's ViewModel with no WinForms layer; MIT allows commercial use.

### 3.3 Behavior: single vs double-click, tooltip, balloon, icon

- Specify `LeftClick` = open/toggle popup, `RightClick` = context menu
  (current Windows convention; `DoubleClick` inherited from the old WinForms
  sample — keep it as an alias for open, never as the only path: touch and
  discoverability penalize double-click). `H.NotifyIcon` lets you declare this
  (`MenuActivation`/`PopupActivation`/`LeftClickCommand`).
  ([hardcodet/wpf-notifyicon — README](https://github.com/hardcodet/wpf-notifyicon),
  [Add Icons to the TaskBar — DoubleClick sample](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/app-icons-to-the-taskbar-with-wf-notifyicon))
- Tooltip: short fallback text (`ToolTipText`); standard Windows balloon
  for warnings (e.g. hotkey already taken §1.5) via `ShowBalloonTip`.
  ([NotifyIcon Component Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms),
  [NotifyIcon.ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0))
- Icon: multi-size `.ico` (16/24/32/48/256); `IconSource` accepts `.ico`
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md)).
  Dark/light: provide `.ico` variants and swap `IconSource` when the app theme
  changes (H.NotifyIcon's Light/Dark `ContextMenuThemeMode` covers the
  native menu in `PopupMenu` mode — see the readme's WinUI context menu section).

## 4. Popup

### 4.1 Window: `ShowActivated=false` + `Topmost`

```xml
<!-- XAML -->
<Window x:Class="WindowsCM.PopupWindow"
        ShowActivated="False"
        Topmost="True"
        ShowInTaskbar="False"
        WindowStyle="None"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight"
        Deactivated="Popup_Deactivated"
        PreviewKeyDown="Popup_PreviewKeyDown" />
```

- `ShowActivated` = whether the window is activated when it is first shown;
  `false` + a dedicated "open without activating" sample.
  ([Window Class — ShowActivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window?view=windowsdesktop-10.0),
  [WPF-Samples ShowWindowWithoutActivation](https://github.com/microsoft/WPF-Samples/blob/main/Windows/ShowWindowWithoutActivation/README.md))
- `Topmost=true` = above all windows with `Topmost=false` (within the
  topmost group, the active one is on top).
  ([Window.Topmost](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=windowsdesktop-10.0))
- Focus loss → auto-hide: handle `Deactivated` (fires on deactivation;
  `IsActive` reports the state).
  ([Window.Deactivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.deactivated?view=windowsdesktop-10.0),
  [Application Management Overview — Activated/Deactivated](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview))
  Product decision (Copyous parity): `Deactivated → Hide()` with a future
  configurable exception; `Esc` closes (`Key.Escape → Hide()` in `PreviewKeyDown`).
- ~150 ms animation: `Popup`/`Window` with a `Storyboard` (`DoubleAnimation` on
  `Opacity` + `TranslateTransform`), equivalent to Copyous's close-animation;
  based on
  ([Popup — WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup),
  [Animate a Popup](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)).
  The exact duration is a parity decision, not an API one.

### 4.2 Position: `GetCursorPos` (mouse) vs caret (text)

- `GetCursorPos` returns the mouse position **in screen coordinates**
  (not affected by the mapping mode).
  ([GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos))
  → WindowsCM's v1 default: open under the cursor. Simple, global, no
  special permission beyond `WINSTA_READATTRIBUTES`/input desktop
  (same page, Remarks).
- `GetCaretPos` returns the caret **in client coordinates of the window that
  contains the caret** and **does not participate in DPI virtualization**
  (logical values of the owning window; the calling thread is ignored).
  ([GetCaretPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcaretpos))
  In practice it only works for the thread's own caret → for a **global** caret
  use `GetGUIThreadInfo`.
- `GetGUIThreadInfo(idThread, &gui)`: with `idThread = NULL` it returns info for
  the **foreground thread**; it works even if the active window belongs to
  another process; it provides `hwndCaret` + `rcCaret` (the caret's bounding
  rect, in client coords of `hwndCaret`) — convert with `ClientToScreen`.
  ([GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo),
  [GUITHREADINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-guithreadinfo))
  Caveats from the docs themselves: it may not return valid handles while the
  window is losing activation; for an edit control, `rcCaret` includes text
  direction/padding (the exact position may need adjusting).
  ([GetGUIThreadInfo — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo))
- Modern alternative: UI Automation — `AutomationElement.FocusedElement`
  ([FocusedElement](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.focusedelement?view=windowsdesktop-10.0)) /
  `IUIAutomation::GetFocusedElement`
  ([Win32](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation-getfocusedelement))
  + the element's `TextPattern`
  ([TextPattern](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern?view=windowsdesktop-10.0)).
  Caret range via `IUIAutomationTextPattern2::GetCaretRange` **not verified
  in this research** (gap §6.3).
- Decision: v1 = cursor (`GetCursorPos`); v2 = try the caret via
  `GetGUIThreadInfo(0)` + `ClientToScreen`, with a fallback to the cursor when
  invalid. UIA is left as a future evolution.

### 4.3 Multi-monitor + per-monitor DPI (sample with DPI)

- Monitors: `MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST)` returns the
  `HMONITOR` of the point in virtual-screen coordinates.
  ([MonitorFromPoint](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfrompoint))
  Virtual desktop metrics: `SM_X/Y/CX/CYVIRTUALSCREEN`, `SM_CMONITORS`.
  ([Multiple Monitor System Metrics](https://learn.microsoft.com/en-us/windows/win32/gdi/multiple-monitor-system-metrics))
  Managed side: `Screen.FromPoint` returns the screen of the point (or the
  nearest one) with `Bounds`/`WorkingArea` to clamp the popup.
  ([Screen.FromPoint](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.frompoint?view=windowsdesktop-10.0))
- DPI: declare per-monitor in the manifest (`dpiAwareness` PerMonitor) —
  WPF is system-DPI-aware by default and needs an opt-in.
  ([WPF-Samples PerMonitorDPI](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md))
  For an already-created process, `SetProcessDpiAwarenessContext` (recommended
  via the manifest; call it before any UI; PerMonitorV2 = Win10 1703+).
  ([SetProcessDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setprocessdpiawarenesscontext),
  [DPI_AWARENESS_CONTEXT](https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context))
  px→DIPs conversion at the placement point:
  `VisualTreeHelper.GetDpi(visual)` → `DpiScale`.
  ([VisualTreeHelper.GetDpi](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visualtreehelper.getdpi?view=windowsdesktop-10.0))

```csharp
// cs: place the popup at the cursor, with DPI and monitor clamping (minimal)
[DllImport("user32.dll", SetLastError = true)]
static extern bool GetCursorPos(out POINT lpPoint);
[StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

void PlaceAtCursor(Window popup)
{
    GetCursorPos(out var pt); // screen px — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
    var src = PresentationSource.FromVisual(popup);
    double dx = 1, dy = 1;
    if (src?.CompositionTarget != null) // physical px → DIPs
    {
        var m = src.CompositionTarget.TransformFromDevice;
        dx = m.M11; dy = m.M22;
    }
    // Per-visual alternative: VisualTreeHelper.GetDpi(popup).DpiScaleX/Y
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visualtreehelper.getdpi?view=windowsdesktop-10.0
    var area = System.Windows.Forms.Screen.FromPoint(
        new System.Drawing.Point(pt.X, pt.Y)).WorkingArea; // the point's monitor
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.frompoint?view=windowsdesktop-10.0
    popup.Left = Math.Min(pt.X * dx, (area.Right - popup.Width) * dx);
    popup.Top = Math.Min(pt.Y * dy, (area.Bottom - popup.Height) * dy);
    popup.Left = Math.Max(popup.Left, area.Left * dx);
    popup.Top = Math.Max(popup.Top, area.Top * dy);
}
```

## 5. Copyous internal shortcuts → WPF (`KeyBinding`/`InputBinding`)

WPF basis: `KeyBinding` binds a `KeyGesture` to an `ICommand`
([KeyBinding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.keybinding?view=windowsdesktop-10.0),
syntax `Gesture="CTRL+R"` or `Key`+`Modifiers`)
in `Window.InputBindings`/`UIElement.InputBindings`
([InputBinding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.inputbinding?view=windowsdesktop-10.0)).
Preview events tunnel (root→target) before the bubbling ones — `PreviewKeyDown`
on the outer container runs before the focused control; composite controls can
mark the bubbling event as handled (e.g. `ButtonBase` marks `MouseLeftButtonDown`
and raises `Click`).
([Input Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview),
[Preview events](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/preview-events)).
Routed commands have default bindings (e.g. `CTRL+C` comes with Copy) and
`TextBox` already ships an internal `CommandBinding` for editing (Paste/Copy/Cut/Undo…).
([Commanding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview),
[Hook Up a Command](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/how-to-hook-up-a-command-to-a-control-with-command-support)).

| Copyous | WPF proposal | Collision / resolution |
|---|---|---|
| `Enter`/`Space` copy-vs-paste, `Shift` inverts, `swap-copy-shortcut` | `PreviewKeyDown` on the `Window`/list; a bool setting inverts | `TextBox` does not support formatting commands but supports basic ones such as `MoveToLineEnd` ([TextBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/textbox)); the internal `TextEditor` handles `Space`/`Shift+Space` before `KeyDown` ([dotnet/wpf#8249](https://github.com/dotnet/wpf/issues/8249)). Only handle Enter/Space when focus is **not** in the search box (`Keyboard.FocusedElement is not TextBox`) |
| `Ctrl+Enter` default action | `KeyBinding Ctrl+Enter` on the `Window` | In a multiline `TextBox` (`AcceptsReturn`) Enter inserts a line break; the popup uses a single-line search box → safe; reinforce with `PreviewKeyDown` if needed |
| `Ctrl+S` pin | `KeyBinding Ctrl+S` | No standard editing binding in `TextBox`; safe at window scope |
| `Delete` (＋`Shift` forces) | `PreviewKeyDown` (Delete/Shift+Delete) | `Shift+Delete` = Cut in text controls (default Cut gesture); `Delete` deletes a char in the search box. Scope to the focused list, never global |
| `Ctrl+E` edit | `KeyBinding Ctrl+E` | `TextBox` does not implement formatting commands (`ToggleBold` etc.) ([TextBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/textbox)); free |
| `Ctrl+T` title | `KeyBinding Ctrl+T` | No known default; free |
| `Ctrl+A` menu | `PreviewKeyDown` scoped to the list | `Ctrl+A` = SelectAll inside `TextBox` (default binding of the ApplicationCommands family — **confirm the exact gesture on the command's page**, gap §6.4). Do not hijack it when the search box has focus |
| `Ctrl+0..9` jump | `KeyBinding` ×10 on the `Window` | No defaults; note: `Key.D0..D9` + also the numpad (`NumPad0..9`) if you want to cover it |
| `Alt` (alone) toggle pinned | **Recommended: replace with `Alt+P`** | The Alt key alone generates `Key.System` and triggers menu mode/menu focus ([Input Overview — Key.System / TextInput](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview)); a bare Alt steals focus and breaks accessibility |
| `Ctrl+F` search | `KeyBinding Ctrl+F` → focus on the search box | `Find` exists in the ApplicationCommands family but with no built-in UI in `TextBox` ([Commanding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview)); safe |
| `Ctrl+Tab` type | `KeyBinding` or `PreviewKeyDown` | Tab moves focus; `Ctrl+Tab` in a `TextBox` with `AcceptsTab` inserts a tab. Search box with `AcceptsTab=false` → safe |
| `Ctrl+\`` tag | `KeyBinding Key=Oem3` (or `OemBackquote` depending on layout) | No default; note the layout dependency (`` ` `` varies by ABNT/US keyboard) — expose it in the hotkey editor |
| `Ctrl+Shift+0..9` tag | `KeyBinding` ×10 | No defaults; free |
| Scroll | keep the default `ScrollViewer` | No custom binding; parity = do not intercept the wheel |

General rule: whatever is **global-in-the-popup** (`Ctrl+S/E/T/F`, `Ctrl+0..9`,
`Ctrl+Shift+0..9`, `Ctrl+Enter`) goes in `Window.InputBindings`; whatever depends
on **where the focus is** (`Enter`, `Space`, `Delete`, `Ctrl+A`) goes in a
tunneled `PreviewKeyDown` on the `Window` checking `Keyboard.FocusedElement`,
because preview runs before the control and `e.Handled = true` prevents the
default behavior.

```xml
<!-- XAML: global-in-the-popup bindings -->
<Window.InputBindings>
  <KeyBinding Gesture="CTRL+S" Command="{Binding TogglePinCommand}" />
  <KeyBinding Gesture="CTRL+E" Command="{Binding EditCommand}" />
  <KeyBinding Gesture="CTRL+T" Command="{Binding EditTitleCommand}" />
  <KeyBinding Gesture="CTRL+F" Command="{Binding FocusSearchCommand}" />
  <KeyBinding Gesture="CTRL+ENTER" Command="{Binding DefaultActionCommand}" />
  <KeyBinding Gesture="CTRL+1" Command="{Binding JumpCommand}" CommandParameter="1" />
</Window.InputBindings>
```

```csharp
// cs: focus-sensitive (Enter/Space/Delete/Ctrl+A) — preview tunnels before the TextBox
// https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview
void Popup_PreviewKeyDown(object sender, KeyEventArgs e)
{
    bool inSearch = Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    if (inSearch) return; // the search box keeps the default editing behavior
    bool swap = Properties.Settings.Default.SwapCopyShortcut;
    if (e.Key == Key.Enter) { /* Enter=copy / Shift+Enter=paste (or inverted) */ e.Handled = true; }
    else if (e.Key == Key.Space) { /* ditto */ e.Handled = true; }
    else if (e.Key == Key.Delete) { /* Delete / Shift+Delete=force */ e.Handled = true; }
    else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control) { /* menu */ e.Handled = true; }
    else if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
}
```

## 6. Gaps (to confirm during implementation)

1. Numeric value of `ERROR_HOTKEY_ALREADY_REGISTERED` (cite `winerror.h` /
   "System Error Codes (1300-1699)"; we use 1409 as a hypothesis to verify).
2. Current version of the `H.NotifyIcon.Wpf` package on NuGet and the supported
   TFMs (.NET 8/9) — check on nuget.org at the time of referencing it.
3. `IUIAutomationTextPattern2::GetCaretRange` for a global caret via UIA —
   unverified alternative; v1 stays on `GetGUIThreadInfo`.
4. Exact default gestures of `ApplicationCommands`/`EditingCommands`
   (`SelectAll`, `Delete`, `Find`) on each command's page — check before
   finalizing the §5 collision table.
5. Tray tooltip character limit (`NOTIFYICONDATA.szTip`) and balloon behavior
   on current Win11 — confirm in `NOTIFYICONDATA` + manual test.
6. `Ctrl+Shift+V` in browsers (paste without formatting) not verified in each
   browser's primary docs — practical impact the same as VS Code (§2.1), but no
   primary link collected.
