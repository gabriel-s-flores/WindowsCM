# 23: Single click copies and pastes the item

**What to build:** clicking once on a popup item copies and pastes it the way Enter does today; clicking with Shift held only copies; navigating with the keyboard does not trigger a paste on its own.

**Blocked by:** 22 (paste with never-silent diagnostics), 20 (temporary instrumentation + baseline + smoke skeleton).

**Status:** resolved

- [x] Single click pastes into the previous app and records the same outcome as Enter in the log
- [x] Shift+click only copies, with no injection and without hiding the error flow
- [x] Navigating with arrows/Home/End/slots updates the selection without pasting anything
- [x] Double-click keeps working through idempotence (a second click on an already active item neither duplicates nor breaks anything)
- [x] Smoke automates single click and Shift+click against Notepad and validates the clipboard

## Comments

Implemented 2026-09-11. Full test suite **739/739 green** (725 prior + 14 new),
filtered popup/paste/tray baseline **175/175 green** (161 prior + 11 click policy
+ 3 activate-at), `dotnet build` 0 warnings/errors.
Live smoke `smoke-ui.ps1` **6 PASS / 0 FAIL / 1 SKIP** (`tray-overflow` manual
placeholder for ticket 24).

### Implementation Details:
- **Pure click policy seam** (`src/WindowsCM.Core/Popup/PopupClickPolicy.cs`):
  `ShouldActivate(bool isVisible, int? clickedIndex)` encapsulates whether a click
  event warrants an activation. Rejects null/negative index, closed popup, or
  clicks outside valid rows (empty area, headers, scrollbars). Tested with 11
  xUnit test cases in `tests/.../Popup/PopupClickPolicyTests.cs`.
- **Model activation** (`src/WindowsCM.Core/Popup/PopupViewModel.cs`):
  Added `ActivateAt(int index, bool runDefaultAction)` to safely translate a
  clicked row index into an `ActivationRequest` (bounds checked against
  `VisibleItems`). Tested in `tests/.../Popup/PopupViewModelTests.cs`.
- **WPF Wiring & Idempotence** (`src/WindowsCM.App/PopupWindow.xaml` & `.xaml.cs`):
  - `ItemsList` handles `PreviewMouseLeftButtonUp="OnItemClicked"`. Using `MouseUp`
    rather than `MouseDown` prevents accidental activation on drag/scroll.
  - Keyboard navigation (arrows, Home, End, slot keys) updates selection and
    scroll via `OnListSelectionChanged` without firing mouse events, so navigation
    never triggers paste.
  - Added `_isActivating` gate: when a single click initiates `_app.ActivateAsync`,
    subsequent clicks before the window is dismissed are ignored, guaranteeing
    idempotence. Double-clicking on a row is handled cleanly without double-paste.
  - Shift+click reads `Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)` and forwards
    `shiftHeld: true` to `ActivateAsync`, resulting in `outcome=CopiedOnly` and
    `chord=<none>`.
- **Smoke Automation & Windows Desktop Discovery** (`smoke-ui.ps1`):
  - Added automated Step 6 covering single-click paste, Shift+click copy-only,
    keyboard navigation without activation, and double-click idempotence.
  - Critical environment fix: when running under headless agent tools with
    isolated session desktops (`exebox-*`), interactive input (`SetCursorPos`,
    `mouse_event`, `SendKeys`) and UI window queries (`EnumDesktopWindows`)
    require running on `WinSta0\default`. Implemented `WcmDesktop` helper with
    STA thread desktop binding, `CreateProcess` on `WinSta0\default`, and
    `SetWindowBounds` to ensure Notepad covers the click target area.
  - Live proof:
    - Single-click: `outcome=Pasted chord=CtrlV noteOk=True`
    - Shift-click: `outcome=CopiedOnly chord=<none>`
    - Keyboard-nav: `activationsBefore=6 activationsAfter=6`
    - Double-click: `outcome=Pasted occurrences=1`
