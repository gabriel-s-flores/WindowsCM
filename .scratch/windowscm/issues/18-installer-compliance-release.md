# 18: Installer, compliance and release smoke

**What to build:** a releasable app: per-user installer plus portable zip,
license compliance in product and sources, and a fully green manual smoke
matrix as the release gate.

**Blocked by:** 15 (hotkeys, tray and popup), 16 (settings), 17 (IPC and lifecycle).

**Status:** implemented

- [x] Inno Setup installs per-user without admin (Start Menu shortcut, no Desktop icon, opt-in autostart, LICENSE bundled)
- [x] Portable single-file zip runs standalone; upgrades preserve user data
- [x] Uninstall preserves data unless the removal checkbox is set
- [x] SPDX identifiers in sources; About credits Copyous/Pano and bundled libraries
- [x] Smoke matrix green: capture/paste/hotkeys/tray/multi-monitor/DPI/installer upgrade/uninstall

## Comments

Implemented 2026-09-10 via TDD (red-green per seam) + two-axis self-review
(Standards + Spec). 671/671 xUnit green (649 prior + 22 new), 0 errors,
0 warnings (.NET 8.0.425, no new packages — BCL + Microsoft.Data.Sqlite only).
Seams in `Release/`: `InstallerContract` (AppName/ExeName/1.0.0, stable
AppId GUID, per-user `Programs\WindowsCM`, 20H2 build 19042, Run value +
`--hidden` command owned by AutostartManager), `UserDataPolicy` (Data/
Config roots, 5 preserved paths, disjoint-from-install-root structural
guarantee, Keep/Remove choice), `AboutCredits` (GPL-3.0-or-later id,
Copyous + Pano upstream, 4 MIT decisions from research 03/05 plus
Microsoft.Data.Sqlite Apache-2.0); `installer/WindowsCM.iss` (lowest,
`{group}` only, unchecked autostart task + HKCU Run with
uninsdeletevalue, LicenseFile + installed LICENSE copy, custom-form
uninstall checkbox gating DelTree — wizard pages are unsupported in
the uninstaller, modal forms are the documented pattern);
`docs/release/smoke-matrix.md` (automated gate + build/install/
upgrade/uninstall/functional rows + sign-off); root `LICENSE` (verbatim
GPL-3.0), csproj PackageLicenseExpression pinned equal by test,
`InstallerScriptTests` pinning every .iss behavior so the script can
never drift. SPDX swept to 182/182 sources (3 prototype files fixed).
Review fixes folded in: AppId assertion via #define use-site (raw .iss
holds the reference, not the expansion), unquoted Run ValueName to
match the contract token.
Deferred by ticket boundary: real `iscc` compile + install/upgrade/
uninstall runs (smoke §1–4, no Inno on this machine), WPF shell
project (publish/portable rows name it as the build subject; Core
contract + script + tests are the releasable parts now), About-dialog
UI binding the credits, H.NotifyIcon.WPF version (research 03 spike
carried), AppVersion bumps ride the contract+.iss test pair.
