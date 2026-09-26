# Auto-paste into the selected field

Status: resolved
Type: feature
Blocked by: performance-stability-audit (stacked branch)

## Request

When the user invokes WindowsCM after clicking in an area where pasting is
possible, the chosen item should be pasted into that area. On by default,
with an option to turn it off from the menu.

## Previous situation

- Picking an item (Enter/click) already copied it and injected Ctrl+V into
  the window that was focused when the popup was requested, but there was no
  way to turn that off (only Shift, item by item).
- Opened **from the tray**, the window focused at that moment is the taskbar
  (or the tray menu itself): the paste aimed at the taskbar and the item
  never reached the field the user had clicked.
- If the last click was on the desktop, Ctrl+V was injected right there (in
  Explorer that can paste files).

## Solution

- `BehaviorSettings.AutoPaste` (default `true`; old files load `true`) →
  `PasteOptions.AutoPaste`.
- Tray menu: a checkable **Paste automatically** item, next to incognito
  mode; applies immediately and saves. Settings → General: a card with the
  same toggle; the two stay in sync.
- `PasteTargetPolicy` (Core, pure): classifies the focused window (app,
  WindowsCM itself, taskbar/tray/Start, desktop) and decides the target.
  `Win32ForegroundTracker` remembers the last window the user was in
  (`EVENT_SYSTEM_FOREGROUND` hook), so that opening from the tray pastes into
  the app from before.
- `PasteOrchestrator`: with `AutoPaste` off, or with no pasteable target, it
  copies, closes the popup and injects no key at all (toast
  "Added to clipboard"). Shift+Enter still only copies, without closing.

## Known limitation

Detection is per window, not per control: "where pasting is possible" = an
application window (not the desktop or the taskbar). Knowing whether the
focused control inside the app is editable would require UI Automation, with
a risk of false negatives in apps that do not expose it — it would stop
pasting into valid fields.
