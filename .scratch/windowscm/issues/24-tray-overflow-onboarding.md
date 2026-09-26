# 24: Tray validated in the Windows 11 tray overflow + onboarding

**What to build:** the tray is confirmed as present in the Windows 11 tray overflow with a working five-item menu, visible copy feedback and clear guidance on how to drag the icon out of the tray overflow — without attempting programmatic promotion.

**Blocked by:** 20 (temporary instrumentation + baseline + smoke skeleton).

**Status:** resolved

- [x] The single process keeps the icon visible with a tooltip, left click toggles the popup and right click opens Open/Incognito/Clear/Settings/Exit
- [x] Copy feedback (balloon + icon flash) works with the icon inside the tray overflow
- [x] Settings/Diagnostics guides the user to drag the icon out of the tray overflow; no attempt at automatic promotion
- [x] Smoke validates the live process, the tray-visible log and a screenshot of the tray overflow

## Comments

Implemented 2026-09-11. Full test suite **743/743 green** (739 prior + 4 new),
filtered popup/paste/tray baseline **179/179 green** (175 prior + 4 tray-onboarding),
`dotnet build` 0 warnings/errors.
Live smoke `smoke-ui.ps1` **7 PASS / 0 FAIL / 0 SKIP** (all tickets 21–24 automated!).

### Implementation Details:
- **Tray Onboarding Guidance Seam** (`src/WindowsCM.Core/Tray/TrayOnboarding.cs`):
  `TrayOnboarding.Guidance` provides the canonical platform onboarding for Windows 11:
  instructing users that tray icons start inside the overflow flyout (^) by default and
  guiding them to drag the icon onto the taskbar (or enable it in Windows Settings),
  while explicitly documenting that programmatic promotion is not attempted by design
  to preserve Windows platform integrity and stability. Tested in `TrayOnboardingTests.cs`.
- **Settings / Diagnostics Integration** (`src/WindowsCM.Core/Settings/DiagnosticsInfo.cs` & `src/WindowsCM.App/SettingsWindow.xaml`/`.xaml.cs`):
  `DiagnosticsInfo.Collect` now exposes `TrayGuidance`, and `SettingsWindow` displays it
  under a dedicated "System Tray" section with auto-wrapping guidance text. Unit-tested
  in `FoldersDiagnosticsTests.cs`.
- **Tray Observability & Feedback** (`src/WindowsCM.App/TrayManager.cs`):
  Added `TempSmokeLog` logging for tray gestures, menu item selection, copy-feedback flash
  (`[WCM20:tray] flash times=3 intervalMs=65`), and balloon notifications (`[WCM20:tray] balloon title=...`).
- **Automated Smoke Step 7 & Tray Overflow Capture** (`smoke-ui.ps1`):
  - Added automated Step 7 validating:
    1. Single-instance alive and tray icon visible with tooltip `WindowsCM`.
    2. Copy feedback inside the drawer: fresh clipboard copy triggers icon flash (3x65ms)
       and balloon tip notifications.
    3. Settings/Diagnostics guidance validation (contains `overflow`, `drag`, and disclaims programmatic promotion).
    4. Taskbar overflow drawer discovery via `TopLevelWindowForOverflowXamlIsland` / `NotifyIconOverflowWindow`,
       clicks the taskbar chevron (`1670, 1056`), validates that the drawer opens (`opened=True`), captures both
       full-desktop screenshot (`WindowsCM-tray-overflow.png`) and cropped drawer screenshot
       (`WindowsCM-tray-overflow-cropped.png`), and closes the drawer cleanly.
  - Live proof:
    `tray-icon-visible: pid=20300 alive=True trayLogCount=2 tooltip=WindowsCM | copy-feedback: flashCount=21 (new=True) balloonCount=2 | onboarding-guidance: overflow=True drag=True noProgrammaticPromo=True | overflow-drawer-screenshot: hwnd=0x6E079E opened=True closed=True fullShot=True cropShot=True`

