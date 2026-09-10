# WindowsCM

Windows clipboard manager with full parity to GNOME Copyous: history, pins, tags, actions, global hotkeys, and tray.

## Language

### Clipboard domain

**Item**:
A single clipboard record as the user sees and manipulates it: text, code, image, file(s), link, character, or color.
_Avoid_: entry (code/database only), clip, registro

**Item type**:
One of the eight detected kinds: Text, Code, Image, File, Files, Link, Character, Color.
_Avoid_: format, kind, MIME (a MIME is evidence, not the type)

**Pin**:
Marking an item as favorite so history limits and clears never remove it.
_Avoid_: star, favorite (verb), fixar

**Tag**:
One of nine colored labels grouping items. Orthogonal to pin: a tagged item is still removed by limits unless also pinned or protected.
_Avoid_: label, category, etiqueta

**Action**:
A user-defined operation run against an item's content (command, color-conversion, or QR-code kinds), matched by item type and regex.
_Avoid_: script, plugin, command (means the command kind only)

**Incognito mode**:
A toggle suspending capture: while on, nothing copied enters history.
_Avoid_: private mode, pause

**History**:
The time-ordered collection of items, bounded by length and age limits, minus pinned/tagged protection rules.
_Avoid_: log, feed
