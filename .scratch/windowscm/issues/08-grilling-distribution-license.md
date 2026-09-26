# Distribution + license + to-spec readiness

Type: grilling
Status: resolved
Blocked by: 02, 03

## Question

Settle with the human the last items so `to-spec` can run: Inno Setup installer (portable single-file, upgrade, clean uninstall?), MSIX deferred and documented in Out of scope, autostart `Run` + `--hidden`, single-instance `Mutex`, target Win10 20H2+/Win11, GPL-3.0-or-later (headers, NOTICE, attribution to Copyous/Pano), and readiness checklist (all 01–07 resolved? fog cleared?).

Only start when `02` and `03` are `resolved`. Call Skill `grilling` + `domain-modeling`.

## Answer

HITL decisions (accepted as recommended) + readiness confirmed. No ADRs.

- Q16 installer: Inno **per-user** (no admin), `%LocalAppData%\Programs\WindowsCM`,
  Start Menu shortcut (no Desktop by default), autostart **opt-in** (`Run` +
  `--hidden`, installer checkbox + Settings toggle), portable = zipped
  single-file, upgrade preserves data, **uninstall keeps DB/settings** (removal
  checkbox, unchecked).
- Q17 GPL-3.0: root `LICENSE` + a copy in the installer, SPDX headers in the
  sources (`implement` phase), About with credits (Copyous/Pano + 4 MIT libs),
  public repo from day 1.
- Q18 readiness: 01–07 resolved, zero fog, `CONTEXT.md`, no pending ADRs —
  **map done, handoff to `to-spec` authorized**.
