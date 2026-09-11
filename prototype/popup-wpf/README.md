# PROTOTYPE (throwaway) — WindowsCM popup, 3 variants

**Question:** does the WPF popup look/behave like Copyous?
Three variants, switchable via the bottom bar, `Ctrl+1/2/3` (anywhere) or
`1/2/3` (when search is not focused), themes via the Theme button or `F6`:

- **A · Cards** — Copyous-faithful: horizontal 250×170 cards, header with
  search pill + pin filter + Clear.
- **B · List** — Compact profile: vertical full-width rows, header + footer.
- **C · Palette** — command-palette style: dense single-line rows (PowerToys
  Run / Raycast feel), deliberately structurally different from A/B.

Mock data: 8 items in memory (one per Copyous type), 8 of 9 tag colors, 1
pinned. **Nothing is real**: no clipboard writes (copy = status line only),
no tray, no persistence, no settings window.

## Run (needs .NET 8 SDK once)

```powershell
winget install Microsoft.DotNet.SDK.8
dotnet run --project prototype\popup-wpf\PopupProto.csproj
```

from the repo root (`C:\Users\gabri\Documents\Projects\WindowsCM`).
The popup opens next to your mouse cursor (frameless, topmost).

## Keys

| Key | Action |
|---|---|
| `Ctrl+1/2/3`, `1/2/3` (outside search), `◀ ▶` | switch variant |
| `F6` / Theme button | Dark → Light → HighContrast |
| `↑ ↓ ← →`, `Tab`/`Shift+Tab`, `Home/End` | move selection (arrows also work inside search) |
| `Enter` | mock copy (status line only) |
| `Ctrl+Enter` | mock default action (copy / paste-as-path / open-with-browser…) |
| `Delete` | mock delete |
| type in search | live filter; `📌` toggles pins-only; `Clear` previews counts |
| `🕶` | mock incognito toggle |
| `Esc` | close |

The bottom status line always shows the full state:
variant · theme · selected n/8 · pins · filter · incognito · last action.

## Known deviations (deliberate, to confirm)

- Search starts focused AND arrows still drive the list (palette-style).
- `Alt+P` nowhere yet — internal-shortcut mapping (`Alt`→`Alt+P` etc. from
  issue 03) is only partially wired here (Enter/Ctrl+Enter/Delete done).
- Emoji icons (📄🔗🎨…) stand in for the real Copyous icon set.
- No caret-position mode yet (cursor-first v1 only, per issue 03).

## React to (what I need back)

1. Winner: A, B, C — or "header of B with rows of C"-style mix?
2. Density/spacing: card size, row height, header — what feels off?
3. Dark theme vs Copyous screenshot — close enough?
4. Keyboard feel: anything missing vs Copyous (pin `Ctrl+S`? jump `Ctrl+0..9`?)?

## Verdict (2026-09-09, user reaction — resolves issue 07)

- Winner: **A (Cards)**.
- Changes for the spec: **Copyous density/spacing 1:1**; **Dark theme**;
  link rows must not leave big blank space above/below (prototype's C-style
  rows waste vertical space); popup must fill horizontal screen space as
  best as possible (no narrow floating box).

Capture: commit this folder to branch `prototype/popup-wpf` (out of main),
point from `.scratch/windowscm/issues/07-prototype-popup-estilo.md`.
