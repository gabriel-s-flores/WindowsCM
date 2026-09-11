# 24: Tray validado na gaveta do Windows 11 + onboarding

**What to build:** o tray é confirmado como presente na gaveta (overflow) do Windows 11 com menu de cinco items funcional, feedback de cópia visível e orientação clara de como arrastar o ícone para fora da gaveta — sem tentar promoção programática.

**Blocked by:** 20 (instrumentação temporária + baseline + esqueleto do smoke).

**Status:** resolved

- [x] Processo único mantém ícone visível com tooltip, clique esquerdo alterna o popup e clique direito abre Abrir/Incognito/Limpar/Settings/Sair
- [x] Feedback de cópia (balão + flash do ícone) funciona com o ícone dentro da gaveta
- [x] Settings/Diagnóstico orienta a arrastar o ícone para fora da gaveta; nenhuma tentativa de promoção automática
- [x] Smoke valida processo vivo, log de tray visível e captura de tela da gaveta

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
- **Automated Smoke Step 7 & Overflow Gaveta Capture** (`smoke-ui.ps1`):
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

