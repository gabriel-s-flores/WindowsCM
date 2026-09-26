# WindowsCM

Windows clipboard manager with full parity to GNOME Copyous: history, pins, tags, actions, global hotkeys, and tray.

## Language

### Clipboard domain

**Item**:
A single clipboard record as the user sees and manipulates it: text, code, image, file(s), link, character, or color.
_Avoid_: entry (code/database only), clip

**Item type**:
One of the eight detected kinds: Text, Code, Image, File, Files, Link, Character, Color.
_Avoid_: format, kind, MIME (a MIME is evidence, not the type)

**Pin**:
Marking an item as favorite so history limits and clears never remove it.
_Avoid_: star, favorite (verb)

**File category**:
A semantic sub-classification for File and Files items (e.g. Images, Audio, Video, Documents, Spreadsheets, Presentations, Code, Archives), mapping file extensions to customized labels and accent colors, with fallback to Windows PerceivedType associations.
_Avoid_: file format, MIME type, file tag

**Tag**:
Legacy feature originally representing one of nine colored labels manually assigned to items. Retired from the interactive UI and context menus in favor of semantic item types and file categories; preserved as an optional database column for historical compatibility.
_Avoid_: label, category

**Action**:
A user-defined operation run against an item's content (command, color-conversion, or QR-code kinds), matched by item type and regex.
_Avoid_: script, plugin, command (means the command kind only)

**Incognito mode**:
An isolated ephemeral clipboard session: while active, clips copied are stored in-memory only and are visible/pasteable in the popup. Visibility gestures (showing, hiding, tray clicks, or toggling with hotkeys) never deactivate the session. Deactivation requires explicit user action (e.g. exit button or tray menu toggle), upon which the entire incognito clipboard is cleared immediately with zero traces left on disk or in memory.
_Avoid_: pause (it captures in-memory for the duration), permanent storage, auto-clearing on hide

**History**:
The time-ordered collection of items, bounded by length and age limits, minus pinned/tagged protection rules.
_Avoid_: log, feed

**Auto-paste**:
Picking an item pastes it into the field the user had clicked before opening WindowsCM (the paste target), not just onto the clipboard. On by default, toggled from the tray menu or Settings. The paste target is the app that held the focus — or, when WindowsCM is opened from the tray, the last app before the taskbar; when that last place was the desktop there is no target and the pick is only copied.
_Avoid_: paste-on-copy (a dropped Copyous flag), auto-type

**Mobile transfer**:
Bidirectional local Wi-Fi transfer of files (audio, image, documents, video) and text/clipboard between the computer and a mobile device via QR code and an embedded local server.
_Avoid_: cloud sync, pairing, bluetooth

**Compact menu**:
A sleek, lightweight popup opening directly under the mouse cursor for agile paste-and-go workflows, configurable in vertical (320x480px) or horizontal (540x240px) format, equipped with a dedicated top search bar and immediate keyboard navigation.
_Avoid_: sub-window, secondary menu

**Clipboard orientation**:
The spatial axis of the clipboard popup window: `Horizontal` (full-width strip across the display) or `Vertical` (tall side panel filling display height).
_Avoid_: window mode, layout style

**Screen dock position**:
The screen edge to which the large clipboard window anchors: Top or Bottom (when horizontal) and Left or Right (when vertical).
_Avoid_: alignment, gravity

**Large window placement**:
How the large clipboard window is placed: `FollowMouse` (docked to a screen dock position on the monitor under the mouse — the default), `FixedMonitor` (docked on a monitor the user picked, remembered by its device name; the primary stands in while it is disconnected) or `Free` (wherever the user dragged it, at the size they left it, remembered per clipboard orientation and pulled back on-screen if its monitor is gone).
_Avoid_: window mode, floating mode, multi-monitor mode (it does not span monitors)

**Item flow direction**:
The spatial progression of clipboard records based on chronological recency: `RecentOnLeft` vs `RecentOnRight` for horizontal arrangements, and `RecentOnTop` vs `RecentOnBottom` for vertical arrangements.
_Avoid_: reverse mode, flip
