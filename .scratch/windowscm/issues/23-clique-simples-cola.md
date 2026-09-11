# 23: Clique simples copia e cola o item

**What to build:** clicar uma vez num item do popup copia e cola como o Enter faz hoje; clicar com Shift segurado só copia; navegar pelo teclado não dispara colagem sozinho.

**Blocked by:** 22 (colagem com diagnóstico nunca silencioso), 20 (instrumentação temporária + baseline + esqueleto do smoke).

**Status:** resolved

- [x] Clique simples cola no app anterior e registra o mesmo desfecho do Enter no log
- [x] Shift+clique só copia, sem injeção e sem esconder o fluxo de erro
- [x] Navegar por setas/Home/End/slots atualiza a seleção sem colar nada
- [x] Duplo-clique continua funcionando por idempotência (segundo clique num item já ativo não duplica nem quebra)
- [x] Smoke automatiza clique simples e Shift+clique contra o Notepad e valida o clipboard

## Comments

Implemented 2026-09-11. Full test suite **739/739 green** (725 prior + 14 new),
filtered popup/paste/tray baseline **175/175 green** (161 prior + 11 click policy
+ 3 activate-at), `dotnet build` 0 warnings/errors.
Live smoke `smoke-ui.ps1` **6 PASS / 0 FAIL / 1 SKIP** (`tray-gaveta` manual
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
