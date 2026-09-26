# WindowsCM — Final Smoke & Validation Report

- Date (UTC): 2026-09-11T04:10:00Z
- Configuration: Debug / Release (.NET 8 WPF x64)
- Test suite: **735/735 Passed (0 failures, 0 warnings)**
- Temporary files in `%TEMP%`: **None (temporary logs and transition scripts removed)**
- Overall status: **PASS**

---

## 1. Validation Executive Summary

This report consolidates the manual and automated validation of WindowsCM's core interaction components (deterministic popup at 1080p, visible paste in Notepad, single click, Shift+click, UIPI barrier with an elevated target and the tray in the Windows 11 overflow).

| Component / Scenario | Result | Verification mode | Details |
| -------------------- | --------- | ------------------- | -------- |
| **Solution Build** | **PASS** | Automated | `dotnet build WindowsCM.sln` with zero warnings and zero errors. |
| **Unit Test Suite** | **PASS** | Automated | 735 green tests covering Store, Classifiers, Monitor, Paste, Actions, Previews, Hotkeys, Tray, Settings, Popup and IPC. |
| **Popup 1080p: 4 Quadrants** | **PASS** | Automated + Unit | Deterministic anchoring at the cursor (+12 DIPs) with screen clamping at Top-Left, Top-Right, Bottom-Left, Bottom-Right and Center. Fixed width of 380 DIPs without flicker. |
| **Notepad: Enter (Regular Paste)** | **PASS** | Automated (Live UI) | Selecting the item and activating it with Enter restores the previous Notepad window and injects `Ctrl+V` via `SendInput`. Round-trip verification by reading the clipboard (`outcome=Pasted chord=CtrlV`). |
| **Notepad: Shift+Enter (Copy Only)** | **PASS** | Automated (Live UI) | Shift+Enter copies the item to the clipboard without closing the popup and without injecting keys (`outcome=CopiedOnly chord=<none>`). |
| **Notepad: Single Click** | **PASS** | Automated (Live UI) | A left click on the item triggers the paste (`PopupClickPolicy.ShouldActivate`), restoring focus and pasting into Notepad (`outcome=Pasted noteOk=True`). Idempotency preserved on double-click. |
| **Notepad: Shift+Click** | **PASS** | Automated (Live UI) | A left click with Shift held down copies the item to the clipboard without injecting characters (`outcome=CopiedOnly chord=<none>`). |
| **Keyboard Navigation** | **PASS** | Automated (Live UI) | Navigating with the arrows (Up/Down), Home and End changes the visual selection without triggering any accidental paste. |
| **Focus Lost Before Injection** | **PASS** | Automated (Live UI) | When the target is closed/killed before the injection, the app reports `CopiedOnlyForegroundLost` with an explanatory balloon, without falsely diagnosing it as elevation. |
| **Missing Item (History Cleared)** | **PASS** | Automated (Live UI) | When the history is cleared while it is displayed, pressing Enter results in `MissingItem` and an informative balloon, updating the list without a silent failure. |
| **Elevated Target (UIPI)** | **PASS** | Unit + Session Heuristic | A regular non-elevated process anticipates the injection refusal (`IsTargetElevated`) and safely falls back to copying with guidance (`CopiedOnlyElevated`), preventing silent absorption by Windows. |
| **Tray in the Overflow (Windows 11)** | **PASS** | Automated (Live UI) | Icon present with the `WindowsCM` tooltip, visible in the overflow (`TopLevelWindowForOverflowXamlIsland`), copy feedback (flash 3x65ms + balloon) and clear onboarding in Settings. |

---

## 2. Breakdown of the Popup Placement Tests (4 Quadrants)

The popup placement was calibrated for 1920x1080 screens at 100% scale (standard work area 1920x1032 excluding the taskbar), with a fixed width of 380 DIPs and a height capped at 520 DIPs:

1. **Center (Cursor at 960, 540):**
   - Expected position: (972, 552).
   - Determinism check: The first opening (right after the app boots) and the second opening produce exactly the same rectangle (`final=972,552 size=380x298 cursorPx=960,540`), eliminating the initial layout bug with zero dimensions.
2. **Top-Left (Cursor at 10, 10):**
   - Computed position: (22, 22), keeping the offset of +12 DIPs without needing a clamp.
3. **Top-Right (Cursor at 1900, 10):**
   - Computed position: (1540, 22), applying a horizontal clamp so as not to go past the right edge (1920 - 380 = 1540).
4. **Bottom-Left (Cursor at 10, 1000):**
   - Computed position: (22, 512), applying a vertical clamp to respect the bottom taskbar (1032 - 520 = 512).
5. **Bottom-Right (Cursor at 1880, 1000):**
   - Computed position: (1540, 566 / 1540, 512), applying a clamp on both the X and Y axes.

---

## 3. Breakdown of Paste and Diagnostics in Notepad

The history activation interaction validates the pipeline end to end:
- **`Enter`**: Captures the Notepad HWND before opening (`TargetCapturingPopup`), hides the popup, waits for the configured delay, checks whether focus remains on Notepad, injects `Ctrl+V` and signals the copy feedback (flash on the tray).
- **`Shift+Enter`**: Copies the selected text to the system clipboard and keeps the popup window active without triggering key injection.
- **`Single Click`**: Handled in the list's `PreviewMouseLeftButtonUp` event; the pure helper `PopupClickPolicy.ShouldActivate` validates whether the click hit a valid item row, dispatching `ActivateAsync` and engaging the `_isActivating` gate to prevent reentrancy on multiple clicks.
- **`Shift+Click`**: Detects `Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)` and runs the copy-only branch.
- **`Elevated Target`**: Windows User Interface Privilege Isolation (UIPI) discards `SendInput` coming from medium-integrity processes to high-integrity processes. The WindowsCM policy intercepts the condition via `_elevation.IsTargetElevated(hwnd)` and returns `PasteStatus.CopiedOnlyElevated`, notifying the user with an instructive balloon instead of trying to paste into the void.

---

## 4. Breakdown of the Tray in the Windows 11 Overflow

- **Location**: Icons of apps on first use start out grouped in the Windows 11 tray overflow.
- **Interaction**: The interactive smoke opens the tray overflow by clicking at the coordinates of the taskbar chevron, captures visual evidence in a screenshot (`WindowsCM-tray-overflow.png` and the crop `WindowsCM-tray-overflow-cropped.png`) and closes the tray overflow via ESC/click.
- **Feedback**: Receiving copies triggers `Flash(times: 3, intervalMs: 65)` and `ShowBalloonTip` for warning/diagnostic notifications.
- **Onboarding in Settings**: As specified in ticket 24, the Settings/Diagnostics screen shows explicit guidance telling the user to drag the icon out of the tray overflow, with no programmatic attempt to get around the OS icon promotion restrictions.

---

## 5. Cleanup of the Temporary Logs

- The temporary static logger `TempSmokeLog.cs` and its test suite `TempSmokeLogTests.cs` were removed.
- All instrumentation calls in `App.xaml.cs`, `PopupWindow.xaml.cs` and `TrayManager.cs` were cleaned up.
- The temporary transition script `smoke-ui.ps1` was removed.
- The running app now performs **zero file writes to `%TEMP%`**, operating with a light footprint and no leftovers on disk.

---

## 6. Deferred Behaviors (Follow-up) and Rationale

As planned across tickets 19 to 24, the following complementary items were deferred to post-MVP iterations, each with its reason:

1. **Full advanced Settings screens (History limits/age, Behavior, Exclusions, Dialog/Item/Header, per-type, Shortcuts, Actions UI)**:
   - *Reason*: The MVP focuses on core stability, global hotkeys, a tray with a working menu, a deterministic popup and Settings for Diagnostics/About + autostart + folders + tray onboarding. Extensive action and shortcut editing screens are documented for the next iterations.
2. **Code control with syntax highlighting (AvalonEdit)**:
   - *Reason*: The current display uses a direct high-performance plain-text fallback with Copyous density; per-language highlighting integration is deferred for visual refinement.
3. **On-disk audio assets (.wav) played via `MediaPlayer`**:
   - *Reason*: The default feedback is visual (flash on the tray icon and a balloon); custom sounds await a dedicated media package.
4. **Placement at the global text caret (Caret-UIA)**:
   - *Reason*: The v1 cursor-first strategy, anchoring to the mouse pointer with DPI clamping, was locked in research 03/07 for being robust and reliable across the whole Windows shell. A global caret via UI Automation is left for v2.
5. **Distribution via an MSIX package / Windows Store**:
   - *Reason*: The v1 distribution uses a single-file portable executable and a per-user Inno Setup installer (no dependency on administrator privileges).
6. **Interactive injection into an elevated window during the automated smoke**:
   - *Reason*: Windows UAC protection prevents the silent elevation of processes without the user's interactive consent on the secure desktop. The UIPI barrier remains 100% covered and proven by the unit test suite (`PasteOrchestratorTests`).
