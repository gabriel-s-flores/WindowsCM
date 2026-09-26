# Grilling settings + customizations

Type: grilling
Status: resolved
Blocked by: 01

## Question

With the `01` inventory in hand, decide with the human (HITL) the parity of each option on the 4 prefs pages: what goes into the 1.0 spec, Windows defaults, what gets dropped (e.g. `Sync Primary`, `Yaru`, `nautilus`), and the `CONTEXT.md` vocabulary.

Includes General (History/Feedback/Behavior/Exclusions/Locations), Customization (Dialog/Item/Header/Items-per-type/Theme), Shortcuts (7 groups), Actions (CRUD + Default Actions).

Only start when `01` is `resolved`. Call Skill `grilling` + `domain-modeling`, update `CONTEXT.md` inline, propose ADRs only if hard-to-reverse + surprising + a real trade-off.

## Answer

HITL decisions (2 rounds, everything accepted as recommended) + `CONTEXT.md` created at the root (7 terms: item, item type, pin, tag, action, incognito mode, history). No ADRs (nothing hard-to-reverse + surprising + a real trade-off in this batch).

- Q8 GNOME-only cuts: Yaru out; WM_CLASS → process/exe + app picker; `indicator-display` out (the tray covers it); Dependencies → "About/Diagnostics" page (lib versions + open folders in Explorer); Locations stays.
- Q9 profiles: Default + Compact kept (literals from `profiles.ts:164`).
- Q10 History UI: SQLite-only combo (Memory debug-only), `.db` file-chooser, length 10–500 d.50, time 0–1440 d.0, 3 clipboard-history options (keep-pinned-and-tagged default); Behavior same as the original.
- Q11 language + vocabulary: **English UI v1** (PT-BR/i18n out of the spec); **item** = domain/UI, **entry** = code/DB only (in `CONTEXT.md`).
- Q12 sounds: the 9 options + dB slider (ogg→wav conversion is an implementation spike).
- Q13 per-type: all 6 screens in full with mapped defaults (file thumbnail via Shell providers).
- Q14 first run: Default profile, `show-at-pointer` off, show-at-cursor rule kept.
- Q15: middle-click pin, open-behavior toggle, swap-copy, swap-scroll — kept.
