# Large window: fixed monitor and free mode

Status: resolved
Type: feature
Blocked by: auto-paste-toggle (stacked branch)

## Request

Besides docking the large window to the 4 edges of the screen (as today),
have a mode for multiple monitors and a free mode, in which the user drags
the window wherever they want.

## User decisions

- Multiple monitors = **pick a fixed monitor** where the window always opens.
- Free mode = **drag and resize**, remembering position and size.

## Solution

Details in `docs/adr/0006-large-window-placement-modes.md`.
Settings → Layout & Placement → Main Window → **Placement**:
"Docked — monitor under the mouse" (default, same as before),
"Docked — a fixed monitor" (monitor list + **Identify**) and
"Free — drag and resize" (+ **Reset position**).

## Manual smoke (Windows)

- [ ] Fixed monitor with 2 screens: with the mouse on screen 1, the hotkey
      opens the window on the chosen screen, docked to the configured edge.
- [ ] Identify shows "1"/"2" in the center of each screen and disappears in
      2 s without stealing the focus.
- [ ] Disconnect the chosen monitor: the window opens on the primary one;
      reconnect: it goes back to the chosen one.
- [ ] Free: drag by the top handle and by the empty areas of the header;
      resize from the 4 edges and corners; close and reopen at the same place
      and size; horizontal and vertical keep separate rectangles.
- [ ] Free: clicks on cards, buttons and search keep working normally.
- [ ] Reset position recenters the window on the next use.
