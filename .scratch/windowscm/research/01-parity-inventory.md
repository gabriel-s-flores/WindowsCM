# 01 — Copyous parity inventory (primary sources)

Source: shallow clone of `https://github.com/boerdereinar/copyous` at
`C:\Users\gabri\AppData\Local\Temp\opencode\copyous`, commit `fa103e3`
(`fix: prevent grab race condition during close animation (#133)`).
Audit limited to code + schemas + SQL + README. Citations in the format
`copyous:<file>:<line>` refer to that checkout.

## 1. Types, tags, colors, paths, highlight.js (`constants.ts`)

- `ItemType` = Text, Code, Image, File, Files, Link, Character, Color
  (`copyous:src/lib/common/constants.ts:9`, values at `:10-18`;
  `ItemTypes` list at `:22-31`).
- `Tag` = blue, teal, green, yellow, orange, red, pink, purple, slate
  (`copyous:src/lib/common/constants.ts:33`, values at `:34-43`;
  `Tags` list at `:47-57`).
- Tag hex values (SCSS): blue `#3584e4`, teal `#2190a4`, green `#3a944a`,
  yellow `#c88800`, orange `#ed5b00`, red `#e62d42`, pink `#d56199`,
  purple `#9c3cbe`, slate `#6f8396`
  (`copyous:resources/css/themes/default/_copyous-colors.scss:1`; values
  at `:1-9`). The card's insensitive text color derives from variant/contrast
  (`copyous:resources/css/themes/default/_copyous-colors.scss:11`).
- `ActiveState` = None 0, Focus 1, Hover 2, FocusHover 3, Active 4
  (`copyous:src/lib/common/constants.ts:59`).
- `DefaultColors` (custom theme fallback, `[dark, light]`):
  `custom-bg-color` rgb(54,54,58)/rgb(250,250,251),
  `custom-fg-color` rgb(255,255,255)/rgb(34,34,38),
  `custom-card-bg-color` rgb(71,71,76)/rgb(255,255,255),
  `custom-search-bg-color` rgb(71,71,76)/rgb(255,255,255)
  (`copyous:src/lib/common/constants.ts:69`).
- Paths (XDG + uuid): cache `getCachePath`
  (`copyous:src/lib/common/constants.ts:286`), data `getDataPath` (`:290`),
  `images/` child of data `getImagesPath` (`:294`), config `getConfigPath`
  (`:298`), `actions.json` child of config `getActionsConfigPath` (`:302`).
  DB default: Json → `<data>/clipboard.json`, otherwise `<data>/clipboard.db`
  (`copyous:src/lib/common/constants.ts:306`).
- `UserAgent` = `Mozilla/5.0 (compatible; CopyousBot/1.0; +https://github.com/boerdereinar/copyous)`
  (`copyous:src/lib/common/constants.ts:76`).
- highlight.js 11.11.1: 3 CDNs (cdnjs, jsdelivr, unpkg)
  (`copyous:src/lib/common/constants.ts:78`; `package.json:24` pins
  `"highlight.js": "11.11.1"` in `copyous:package.json:24`);
  `HljsUrls` = `<cdn>/highlight.min.js`
  (`copyous:src/lib/common/constants.ts:84`); SHA-512
  `f35f2463…675f1` (`copyous:src/lib/common/constants.ts:86`).
- `HljsLanguages`: 192 rows `[id, Name, sha512]`
  (`copyous:src/lib/common/constants.ts:91`; first at `:92`, last
  `zephir` at `:283`). Resolution: `ext.dir/highlight.min.js` (system) otherwise
  `<data>/highlight.min.js` (`copyous:src/lib/common/constants.ts:312`);
  languages: `ext.dir/languages/<id>.min.js` otherwise `<data>/languages/<id>.min.js`
  (`copyous:src/lib/common/constants.ts:333`); per-language URLs
  `<cdn>/languages/<id>.min.js`
  (`copyous:src/lib/common/constants.ts:343`).

## 2. Clipboard pipeline (`misc/clipboard.ts`)

- Accepted mimetypes: Text `text/plain;charset=utf-8, UTF8_STRING, text/plain, STRING`;
  Image `image/png, image/jxl, image/webp, image/avif, image/jpeg`;
  File `x-special/gnome-copied-files, text/uri-list`; sensitive
  `x-kde-passwordManagerHint` (`copyous:src/lib/misc/clipboard.ts:25`).
- `ContentType` Text 0, Image 1, File 2
  (`copyous:src/lib/misc/clipboard.ts:32`).
- Probe order in `getContent`: **Image > File > Text**
  (`copyous:src/lib/misc/clipboard.ts:306`, `:319`, `:340`).
- Empty/whitespace-only text is discarded (`:344`); 0-byte image → null
  (`:309-315`); empty file → null (`:334-336`). File: first line
  `copy|cut` (case-insensitive) sets the operation, rest = paths; no operation
  → `copy` (`copyous:src/lib/misc/clipboard.ts:324`).
- Dedup MD5: text `MD5(text)`, image `MD5(bytes)`, file
  `MD5(paths without file:// joined by \n)`
  (`copyous:src/lib/misc/clipboard.ts:45`). `prevClipboard=[type, checksum]`
  ignores repeats caused by Copyous itself (`:256-260`); special guard: loss of
  file ownership in Nautilus (File→Text with the same checksum) does not update
  (`:264-266`); `prevClipboard` is set **before** `shouldSave` so that a
  copy made in incognito does not leak later (`:269-274`).
- `shouldSave`: false if the mimetype is sensitive (`:225`), if the focused
  window's `wm_class` is in `wmclass-exclusions` (`:230-234`), or if `incognito`
  (`:236`).
- `convertContent` classification (only what passed `shouldSave` gets in):
  1. Link: `trimmed.startsWith('http') && GLib.uri_is_valid(...)`
     (`copyous:src/lib/misc/clipboard.ts:359`).
  2. Character: `!hasMoreGraphemes(trimmed, maxCharacters)` with
     `Intl.Segmenter` grapheme segmentation (`:58-62`, `:368-371`); the limit comes from
     `get_child('character-item').get_int('max-characters')` (`:368`).
  3. Color: `Color.parse(trimmed)` (`:374-376`; grammar at `color.ts:160-174`,
     named+hex+rgb+hsl+hwb+linear-rgb+xyz+lab+lch+oklab+oklch at `:329-343`).
  4. Code: `slice(0,10000)` (`:379`), `n=max(1,len/100)` (`:380`),
     `highlightAuto` and `relevance/n >= 3` (`:381-382`); metadata
     `{language:{id,name}}` with adjusted capitalization (`:386-388`).
  5. Text fallback (`:393`).
  6. Image: creates `images/` if needed, name `<md5>.<mimetype-ext>`, writes only
     if it does not exist, content = `file://` URI
     (`copyous:src/lib/misc/clipboard.ts:397`).
  7. File: 1 path → `File`, N → `Files` (`\n`-joined), metadata `{operation}`
     (`:421-428`).
- Copying back (`handleEntry`): Text/Code/Link/Character/Color → plain text
  (`:194-199`); Image → reads URI, `content_type_guess`, MD5, `set_content`
  (`:200-214`); File/Files → `split('\n')`, operation forced to `copy` (`:215-219`).
  `sync-primary` duplicates text into PRIMARY (`:116-118`); `update-date-on-copy`
  refreshes `datetime` when copying from history (`:189-191`).
- Paste: `copyContent` + **250 ms** timeout (`:141`), then Shift+Insert, or
  **Ctrl+Shift+Insert in a terminal** (`purpose === TERMINAL`, `:143-155`);
  keyboard via `Clutter.VirtualInputDevice` (`keyboard.ts:12`, press/release at
  `:36-42`, purpose tracking at `:14-20`).

## 3. Database (`gda.ts`, `database.sql`, `json.ts`, `memory.ts`)

- Schema v2: `clipboard_version(id=1, version=2)`
  (`copyous:resources/database/database.sql:59`; insert `:65`;
  `DATABASE_VERSION = 2` in `copyous:src/lib/database/gda.ts:13` and
  `copyous:src/lib/database/json.ts:14`).
- Table `clipboard(id INTEGER PK AUTOINCREMENT UNIQUE, type TEXT NOT NULL,
  content TEXT NOT NULL, pinned BOOLEAN NOT NULL, tag TEXT NULL,
  datetime TIMESTAMP NOT NULL, metadata TEXT NULL, title TEXT NULL,
  UNIQUE(type, content))` (`copyous:resources/database/database.sql:46`;
  UNIQUE at `:55`; the v0 migration creates it without `title`
  at `copyous:src/lib/database/gda.ts:307`, v1 `ALTER TABLE ADD COLUMN title`
  at `:325`).
- `database.sql` also includes a `decrease_time` trigger and test data
  (tags/colors/emoji/links/files/code/text/titles) — **test fixture
  only**, not the production schema
  (`copyous:resources/database/database.sql:67`).
- `ClipboardEntry` GObject: id(readonly), type, content, pinned, tag,
  datetime, metadata, title (`copyous:src/lib/database/database.ts:57`).
  `Metadata` = `CodeMetadata{language{id,name}|null}` | `FileMetadata{operation
  copy|cut}` | `LinkMetadata{title,description,image}`
  (`copyous:src/lib/database/database.ts:11`; `FileOperation` at `:31`).
- `Database` interface: init/clear/close/entries/selectConflict/insert/
  updateProperty/delete/deleteOldest
  (`copyous:src/lib/database/database.ts:112`).
- Ordering: `entries()` sorts by `datetime` (Gda `select_order_by(datetime)`
  at `copyous:src/lib/database/gda.ts:416`; Memory `sort(b-a)` =
  **newest-first** at `copyous:src/lib/database/memory.ts:49`).
- `clear(history)`: `KeepAll` → nothing; `KeepPinnedAndTagged` → deletes only
  `NOT(pinned OR tag IS NOT NULL)`; `Clear` → everything
  (`copyous:src/lib/database/memory.ts:20`; SQL predicate at
  `copyous:src/lib/database/gda.ts:715`).
- Protection works by **survival**, not by separate UI blocking:
  `deleteOldest(offset=N, olderThanMinutes=M)` deletes unprotected items beyond N
  **UNION** unprotected items older than M
  (`copyous:src/lib/database/gda.ts:645`; Memory at
  `copyous:src/lib/database/memory.ts:111`).
- `insert` fails on conflict (UNIQUE), returning null
  (`copyous:src/lib/database/memory.ts:59`; Gda `:525`);
  `updateProperty` on a conflicting `type/content` returns the **id of the
  conflicting item** (`copyous:src/lib/database/gda.ts:599`); Memory only supports
  updating `content` (`copyous:src/lib/database/memory.ts:80`).
- Backends and default: Default order = `in-memory-database`(deprecated) →
  `<name>.db` exists → `<name>.json` exists → try SQLite → try JSON →
  memory (`copyous:src/lib/database/entryTracker.ts:43`; mirrored in the UI at
  `copyous:src/lib/preferences/general/historySettings.ts:194`).
  `getFile()` honors `DEBUG_COPYOUS_DBPATH`, otherwise `database-location` or
  default (`copyous:src/lib/database/entryTracker.ts:99`).
  Gda missing → warning with a `disable-gda-warning` action
  (`copyous:src/lib/database/entryTracker.ts:128`).
- Tracker: `selectConflict` before inserting; if already tracked, it only bumps
  `datetime` and returns null (reorders without duplicating)
  (`copyous:src/lib/database/entryTracker.ts:198`); `deleteOldest` runs on every
  insert and uses `history-length`(N)/`history-time`(M) (`:219-245`);
  `checkOldest` scans in memory (`:225-238`); `track()` subscribes to
  content/pinned/tag/datetime/metadata/title and merges edit conflicts
  (`:247-270`); deleting an Image deletes the file, deleting a Link deletes the
  cached thumbnail (`:272-303`).
- JSON backend: same semantics as Memory + persistence debounced by **1000 ms**
  (`copyous:src/lib/database/json.ts:114`), ISO-8601 UTC
  (read `:61`, write `:134`), `title || undefined` (`:136`), creates the dir
  (`:141-144`). `json.sql` is the SQLite→JSON exporter
  (`version:2`, `strftime('%FT%T.000000Z', datetime,'utc')`)
  (`copyous:resources/database/json.sql:1`).
- Periodic cleanup: `extension.ts` schedules `deleteOldest` every **60 s** when
  `history-time > 0`, postponing it while the dialog is open
  (`copyous:src/extension.ts:272`); Gda escapes `\\`→`\` because of a libgda bug
  (`copyous:src/lib/database/gda.ts:227`).

## 4. Actions (`actions.ts` + prefs)

- Model: `ActionConfig{actions:(Action|ActionSubmenu)[], defaults?: Partial<Record<ItemType,string>>}`
  (`copyous:src/lib/common/actions.ts:13`); submenu `{name, actions}`
  (`:18`); action `{kind, id, name, pattern?, types?, output, shortcut?}`
  (`:31`); `CommandAction{command}` (`:41`), `ColorAction{space,
  types:[Color], output:copy|paste}` (`:46`), `QrCodeAction{output:ignore}`
  (`:53`); `ActionOutput` ignore/copy/paste (`:23`).
- Matching: empty/null `types` = all; `pattern` = `new RegExp(pattern)` with
  `test` (`testAction`, `copyous:src/lib/common/actions.ts:83`) and `match`
  returning groups (`matchAction`, `:102`); invalid regex = no match (`:91-93`,
  `:110-112`). `isDefaultAction`/`findDefaultAction` resolve via
  `defaults[entry.type]` (`:144-157`).
- Execution (`actionMenu.ts`): the command runs `sh -c <command> _ <groups...>`
  with **stdin = content** (`copyous:src/lib/ui/components/actionMenu.ts:217`;
  documented in the UI at `copyous:src/lib/preferences/actions/actionDialog.ts:398`);
  **30 s** timeout (`copyous:src/lib/ui/components/actionMenu.ts:199`);
  stdout `trim`, empty = ignored (`:222-224`); `copy|paste` emit signals
  (`:226-232`); color converts via `Color.toColor(space)` (`:244-259`); QR opens
  a dialog (`:261-266`); the default item gets a DOT ornament (`:99-106`); config
  reloads via FileMonitor (`:131-137`, also at
  `copyous:src/lib/misc/shortcuts.ts:99`).
- Built-in defaults (`defaultConfig`, `copyous:src/lib/common/actions.ts:163`):
  submenu `Open`: `open-with-default` (`xargs xdg-open`, Image+File, `:184`),
  `open-with-files` (`nautilus -s $1`, pattern `^(.*)`, Image+File+Files, `:194`),
  `open-with-browser` (`xargs xdg-open`, Link, `:204`); `paste-as-path`
  (`cut -c8-`, output paste, Image+File+Files, `:217`); submenu `Convert`:
  rgb `^(?!rgb)`, hex `^(?!#)`, hsl `^(?!hsl)`, oklch `^(?!oklch)` (hwb/linear/
  xyz/lab/lch/oklab commented out, `:227-239`), all output paste shortcut [];
  `qrcode` (Text+Code+Link+Character+Color, output ignore, shortcut
  `<Control>q`, `:241-249`). `defaults`: File→`paste-as-path`,
  Files→`paste-as-path`, Link→`open-with-browser` (`:251-255`).
- CRUD in the prefs (`actionsPage.ts`): list (`ActionsGroup`), Default
  Actions page, Restore (merges missing built-ins, keeps customs and
  `config1.defaults`) and Reset (back to `defaultConfig`), both with a backup
  (`copyous:src/lib/preferences/actions/actionsPage.ts:114`; merge at
  `copyous:src/lib/common/actions.ts:345`, `countDifference` badge at `:329`).
  Forms (`actionDialog.ts`): Command requires name+command
  (`:496-498`), outputs Ignore/Copy/Paste (`:454-464`); Color requires a name, 10
  color spaces, outputs Copy/Paste (`:529-555`, `:585-587`); QRCode requires a name,
  fixed output ignore (`:595-650`); new ids = `uuid_string_random`
  (`:476`, `:566`, `:630`); empty `types` = all (`:488`, `:641`).
- The Default Actions page has 1 row per **type (8)** with an applicability
  filter and a None option (`copyous:src/lib/preferences/actions/actionDefaults.ts:99`;
  per-type rows at `:132-167`; `validType` empty=all at `:18`).
- Persistence: `actions.json` (tab-indented, `backup` flag)
  (`copyous:src/lib/common/actions.ts:294`; load with the
  `DEBUG_COPYOUS_ACTIONS` override at `:264-268`).

## 5. Preferences (`prefs.ts` + `gschema.xml` + `preferences/**`)

Pages (`copyous:src/prefs.ts:57`): General (History, Feedback, Behavior,
AppExclusions, Dependencies, Locations at `:80-90`), Customization (Profiles,
Dialog, Item, Header, Items, Theme at `:100-107`), Shortcuts (8 groups at
`:117-124` — Dialog, Item, ItemActivation, PopupMenu, Navigation, Search,
SearchNavigation, SearchScroll), Actions (`:127-129`).

### General
- History (`historySettings.ts:48` + gschema `:113-139`): `database-backend`
  enum default/memory/sqlite/json, default `default` (`:118`);
  `database-location` string default `''` = `<data>/clipboard.{db,json}`
  (`:122`); `clipboard-history` clear/keep-pinned-and-tagged(default)/keep-all
  (`:126`); `history-length` int 10–500 default 50 (`:130`);
  `history-time` int 0–1440 default 0 = no limit (`:135`);
  `in-memory-database` bool default false **deprecated** (`:114`). UI: combo
  Memory/SQLite(recommended)/JSON (`:69-106`), location row with a file-chooser
  (`*.db`/`*.json`, `:267-285`), spins (`:142-156`), gating by Gda
  (`:194-228`).
- Feedback (`feedbackSettings.ts:213` + gschema `:175-215`):
  `indicator-display` hidden/icon-only(default)/content-only/icon+content
  (`:176`); `wiggle-indicator` default true (`:188`, sensitive only with the icon, at
  `:274-280`); `send-notification` default false (`:192`);
  `sound` none/click/hum/string/swing/message/message-new-instant/bell/
  dialog-warning, default none (`:196`); `volume` double −20…+20 dB default 0.0
  (`:211`); `show-indicator`/`show-content-indicator` **deprecated**, migrated
  to `indicator-display` (`settings.ts:544`; gschema `:180-187`). Sound UI:
  radio + dB slider with preview (`feedbackSettings.ts:122`; `:200-204`).
- Behavior (`behaviorSettings.ts:11` + gschema `:141-173`): `remember-search`
  false (`:141`), `exclude-pinned` false (`:146`), `exclude-tagged` false
  (`:150`), `protect-pinned` true (`:154`), `protect-tagged` true (`:158`),
  `sync-primary` false (`:166`), `update-date-on-copy` true (`:170`);
  `paste-on-copy` **deprecated** → migrates inverted to `swap-copy-shortcut`
  (`settings.ts:544`; gschema `:162`).
- Exclusions (`appExclusionSettings.ts:313` + gschema `:217-221`):
  `wmclass-exclusions` strv default [] (`:218`); UI with an app catalog +
  manual WM_CLASS entry (`:25-210`, page at `:290-311`).
- Dependencies/Locations: `disable-gda-warning`/`disable-hljs-dialog` bool
  false (`gschema:104`; `:108`); hljs page with per-language install/uninstall
  (`dependenciesSettings.ts:273`); Locations only opens Data/Config/Cache
  (`locationsGroup.ts:10`).

### Customization
- Profiles: Default vs Compact (literal values at
  `copyous:src/lib/preferences/customization/profiles.ts:164`):
  Default(show-at-pointer F, horizontal, top/fill, size 500, auto-hide F,
  250×170, dynamic F, header T, controls visible, file preview-or-info, link
  vertical); Compact(pointer T, vertical, fill/left, 500, auto-hide T, 300×100,
  dynamic T, header hidden, controls on-hover, file info-only, link
  horizontal).
- Dialog (`dialogCustomization.ts:13` + gschema `:223-287`): `show-at-pointer`
  F (`:224`), `show-at-cursor` F (`:228`), `clipboard-orientation`
  horizontal (`:232`), `clipboard-position-vertical` top (`:236`),
  `clipboard-position-horizontal` fill (`:245`), `clipboard-size` 200–10000
  default 500 (`:254`), margins 0–10000 default 6 (`:260-279`),
  `auto-hide-search` F (`:280`), `show-scrollbar` T (`:284`). The vertical position
  accepts the aliases left→top/right→bottom (gschema `:240-243`, same for horizontal
  `:249-252`).
- Item (`itemCustomization.ts:13` + gschema `:289-308`): `item-width`
  200–1000 default **250** (`:290`), `item-height` 50–1000 default **170**
  (`:295`), `dynamic-item-height` F — vertical only (`:300`, gating at
  `:67-71`), `tab-width` 1–8 default 4 (`:304`).
- Header (`headerCustomization.ts:13` + gschema `:310-322`): `show-header` T
  (`:311`), `header-controls-visibility` visible/on-hover/hidden default
  visible (`:315`), `show-item-title` T (`:319`).
- Items per type (child schemas):
  text (`show-text-info` F, `text-count-mode` characters/words/lines default
  characters; `copyous:resources/schemas/...gschema.xml:386`);
  code (syntax-highlighting T, show-line-numbers T, show-code-info F,
  text-count-mode characters; `:397`);
  image (show-image-info F, background-size cover/contain default cover; `:416`);
  file (visibility preview/file-info/**preview-or-file-info(default)**/
  preview-and-info/hidden `:427`; types flags text|image|thumbnail default
  all `:432`; exclusion glob [] `:435`; background cover `:439`; hl T `:443`;
  lines T `:447`);
  link (preview T `:454`, preview-image T `:458`, image-bg **contain** `:462`,
  orientation **vertical** `:466`, exclusion regex [] `:470`);
  character (max-characters 1–4 default **1** `:476`, show-unicode F `:480`).
- Theme (child `theme`, `:488`): `theme` default/yaru/custom default default
  (`:489`); `color-scheme` system/dark/light/high-contrast default system
  (`:493`); `custom-color-scheme` dark/light default dark (`:497`) — note that
  `settings.ts:349` provides for `HighContrast:2` with no counterpart in the schema;
  4 custom colors, string, default `''` with a `DefaultColors` fallback
  (`:501-516`; logic in `themeCustomization.ts:46`).

### Shortcuts (8 groups in the UI; the schema persists only some)
Persisted (`gschema:335-383`): `open-clipboard-dialog-shortcut`
`[<Super><Shift>v]` (`:336`), `toggle-incognito-mode-shortcut`
`[<Super><Control><Shift>v]` (`:340`),
`open-clipboard-dialog-behavior` toggle/open-or-select-next default toggle
(`:344`), `pin-item-shortcut [<Control>s]` (`:349`),
`delete-item-shortcut [Delete]` (`:353`), `edit-item-shortcut [<Control>e]`
(`:357`), `edit-title-shortcut [<Control>t]` (`:361`),
`open-menu-shortcut [<Control>a]` (`:365`),
`middle-click-action` none/**pin(default)**/delete (`:370`),
`swap-copy-shortcut` F (`:375`), `swap-scroll-shortcut` F (`:380`).
Hardcoded (no key): activation Paste `Return/space`, Copy
`<Shift>Return/space`, default `<Ctrl>Return/space` + swap
(`itemShortcuts.ts:61`; lines `:72-89`); Navigation Tab/arrows/Home/End,
`Ctrl+0..9`, `Ctrl+F` (`navigationShortcuts.ts:9`); Popup `0..9`
(`popupMenuShortcuts.ts:9`); Search `Alt` pinned, `Back` clears tag/type,
`Return` first item (`searchShortcuts.ts:46`); SearchNav
`Ctrl+Tab`/`Ctrl+Shift+Tab`, `` Ctrl+` ``/`Ctrl+Shift+` ``, `Ctrl+Shift+0..9`
(`:57-68`); SearchScroll scroll=cycle type vs `Ctrl+scroll`=cycle tag
(+swap) (`searchEntry.ts:578` + `searchShortcuts.ts:70`).
The README summarizes the main ones (`copyous:README.md:74`; table `:77-89`).

## 6. D-Bus + extension (`dbus.ts`, `extension.ts`)

- XML interface `org.gnome.Shell.Extensions.Copyous`: `Toggle`, `Show`,
  `Hide`, `ClearHistory(in b all)` (`copyous:src/lib/common/dbus.ts:7`).
- Name `org.gnome.Shell.Extensions.Copyous` (session)
  (`copyous:src/lib/common/dbus.ts:48`), object
  `/org/gnome/Shell/Extensions/Copyous` (`:84-87`). Documented in the README
  (`copyous:README.md:91`; table `:95-100`; gdbus examples `:102-114`).
- `ClearHistory(true)` → `ClipboardHistory.Clear`, `false` →
  `KeepPinnedAndTagged` (`copyous:src/lib/common/dbus.ts:72`); GObject signals
  toggle/show/hide/clear-history (`:27-35`).
- Auto-cleanup: `ConfirmedLogout/Reboot/Shutdown` (SessionManager) +
  `PrepareForShutdown` (login1 system) → `emit('clear-history', -1)`
  (`copyous:src/lib/common/dbus.ts:89`; `:100`, `:110`, `:120`); the extension
  translates `-1` → the current `clipboard-history` pref
  (`copyous:src/extension.ts:105`); toggle/show/hide wiring at `:98-108`;
  `destroy` unexport + unsubscribe (`copyous:src/lib/common/dbus.ts:77`).
- Extension lifecycle: settings+migrate → hljs → theme → dialog+indicator → dbus →
  feedback → shortcuts → tracker+DB → clipboard
  (`copyous:src/extension.ts:49`; copy/paste/clear handlers at `:64-108`;
  clipboard→UI/sound/notification at `:145-169`).

## 7. Dialog UI (`clipboardDialog.ts`, `items/*`, `components/*`, `css/**`)

- Structure: vertical `St.BoxLayout` `.clipboard-dialog.horizontal|.vertical`
  with Header / ScrollView / Footer + popup menu
  (`copyous:src/lib/ui/clipboardDialog.ts:293`; children at `:310-341`).
  Header: settings + incognito + central search + Clear
  (`:86-200`); Footer (vertical only, `:689-690`): settings + incognito + Clear
  (`:208-243`). 150 ms animation (`:37`), modal `SYSTEM_MODAL` (`:428`),
  GNOME≥49 scale 0.96 EASE_OUT_QUAD (`:464-470`).
- Layout: `clipboard-orientation` toggles the class and locks width(size) vs
  height(size) (`:674-686`, default 500); position `(pos+1)%4` (`:670-671`);
  `show-at-pointer` centers + FitConstraint at the pointer
  (`:664-668`, `:710-725`); `show-at-cursor` uses IBus with a +4px offset
  (`:715-716`, capture at `:702-708`); margins via inline
  `margin: T R B L` (`:693-700`); header collapsible via `auto-hide-search`
  (`:161-178`); click/tap outside closes (`:812-832`); Esc closes (`:741-744`).
- Search: `SearchQuery{query,pinned,tag,type}` + exclude flags
  (`searchEntry.ts:48`); locale-insensitive substring via `Intl.Collator`
  sensitivity base (`:18-26`); Same/LessStrict/MoreStrict optimization
  (`:84-92`); fallback to `entry.title` (`:91`); popup with All+8 types
  (mnemonics) and Tags (`:131-163`); middle-click clears tag+type (`:303-310`);
  `remember-search=F` clears on unmap (`:598-607`); `exclude-pinned/tagged`
  re-trigger (`:370-376`); dialog keyboard shortcuts at
  `clipboardDialog.ts:737`.
- Scroll: horizontal = hscroll auto/vscroll never and vice versa
  (`clipboardScrollView.ts:110`); `show-scrollbar=F` disables both (`:113-115`);
  fade 12px (`:36-44`); wheel advances `item+spacing` with a 150 ms ease
  (`:180-208`); Home/End (`:141-159`).
- Cards (`clipboardItem.ts:32`): `St.Button` ONE|THREE (+TWO if middle-action,
  `:164-171`); size = prefs (`item-width/height`, `:142-145`); dynamic
  height vertical only, with `max-height` (`:147-154`); background with a GLSL hole for
  the header buttons (`HoleEffect`, `:309-395`); bindings
  pinned/datetime/tag/title (`:80-93`); protection: delete blocked if
  protected, Shift = force (`:190-198`, Delete with Shift at `:260-264`);
  left click activates / Shift activate-alt / Ctrl default-action / middle pin|delete /
  right menu (`:200-229`); keys pin/delete/edit/edit-title/menu/action
  (`:241-290`); swap-copy inverts activate/activate-shift
  (`clipboardDialog.ts:603`).
- Item header (`clipboardItemHeader.ts:84`): icon + title (ellipsis) +
  relative time `formatTimeSpan` (`:45-47`) + pin/menu/delete/tag; title
  editable by double-click with an Entry (Return saves, Esc cancels, focus-out
  saves, empty reverts to the default, custom hides the time)
  (`:143-171`, `:358-433`); controls visible/on-hover(active)/hidden
  (`:435-454`); delete disappears if protected (`:445-448`); CSS class `tag`
  (`:320`).
- Default titles per type: Text (`textItem.ts:24`), Code (`codeItem.ts:23`),
  Image (`imageItem.ts:38`), File (`fileItem.ts:52`), Files
  (`filesItem.ts:140`), Link (`linkItem.ts:303`), Char **"Char"**
  (`characterItem.ts:22`), Color (`colorItem.ts:71`).
- Tags UI: `.tag-button.<tag>` buttons with a center (circle)
  (`tagsItem.ts:11`); paginated scrollable box with arrows (`:42-97`); exclusive
  selection with toggle-off (`:195-208`).
- Colors/themes: `codeLabel.ts` maps `hljs-*` classes to the Adwaita
  Dark (`:84-141`) / Light (`:143-200`) palette via `applyTheme` (`:215-233`); with
  line numbers (`:395-401`). SCSS: variants `default/dark`
  (`dark.scss:1`), `light` (`light.scss:1`), `high-contrast`
  (`high-contrast.scss:1`) + `yaru/` mirrors; widgets
  `_dialog.scss:1` (`.clipboard-item-list` list, `:81-146`),
  `_clipboard-item.scss:22` (`%card`, header `:38`, content `:143`),
  `_search-entry.scss:1`, `_content-preview.scss:1`, `_popupmenu.scss:19`
  (tags `.tag-button` at `:83`), `_indicator.scss`, `_content-info.scss:1`.
- Indicator: modes hidden/icon/content/both
  (`indicator.ts:155`); content = 1st line (text/code/link/char), basename
  (file), "N Files" (files), swatch 32×32 (color), thumb (image)
  (`:181-213`); wiggle 2px/65ms×3 (`:175-179`); menu incognito/clear/settings
  (`:102-107`); left click opens the dialog, middle click opens prefs (`:248-260`).
- Screenshot: `resources/images/screenshot.png` referenced in the README
  (`copyous:README.md:6`); README features (`:9-14`).

## 8. Dependencies

- **GSound** (`sound.ts:1`): enum none/click/hum/string/swing/message/
  message-new-instant/bell/dialog-warning (`:10`);
  GNOME ogg at `<datadir>/sounds/gnome/default/alerts/<name>.ogg` (`:54-65`),
  xdg = `null` (`:68-71`); play with `ATTR_MEDIA_FILENAME` (GNOME) or
  `ATTR_EVENT_ID` (xdg) + `ATTR_CANBERRA_VOLUME` as a dB string (`:90-108`);
  volume comes from the pref (`:85-86`); `tryCreateSoundManager` returns null if
  `gi://GSound` is missing (`:28-35`); per-distro install guide in the dialog
  (`dependencies.ts:354`; Fedora/Arch/Ubuntu/openSUSE strings at `:360-364`).
- **Soup link preview** (`link.ts:1`): `Session{user_agent: UserAgent,
  idle_timeout: 5}` (`:31`, `:186`); Accept html (`:34`) and image (`:189-192`);
  only `text/html` becomes metadata (`:68`), a direct `image/*` becomes `{image:url}`
  with cache (`:42-65`); meta via regex `property|name` + reverse (`:86-90`);
  title `title|og:title|twitter:title|<title>` (`:93-98`), description likewise
  (`:101`), image `image|og:image*|twitter:image` + `parse_relative`
  (`:104-118`); HTML entities decoded (`:134-158`); cache
  `MD5(url)` in `getCachePath` (`:160-168`), a hit skips the download (`:182-183`).
  Link UI: honors `show-link-preview(-image)`, bg, orientation and the exclusion
  regex (`linkItem.ts:335`; `:347-365`).
- **highlight.js**: see §1 + `dependencies.ts:234` (download with SHA-512
  verification and fallback across CDNs, `:284-299`; per language `:301-324`; already-installed
  aborts `:242-245`); `extension.ts:172` imports it dynamically and watches the
  file for auto-load (`:193-207`); languages registered/unregistered by a
  directory FileMonitor (`:209-253`); fallback without hljs = escaped text
  (`codeLabel.ts:382`); prefs show `HljsDialog` the 1st time it is missing, unless
  `disable-hljs-dialog` (`dependencies.ts:462`).
- **GdkPixbuf thumbnails** (`contentPreview.ts:4`): `get_file_info` for the ratio
  (`:63`) and the `missing-image` fallback (`:84-95`); `tryGetThumbnail` scans
  `~/.cache/thumbnails/*/<md5(uri)>.png` (`:202-229`); `getFileType`:
  directory → Directory; thumbnail first; `image/*` before `text/plain`
  (SVG!) then audio/video/text (`:264-286`); text = first **4096 bytes**
  (`:189-195`); `ImageInfo` with dimensions (`contentInfo.ts:275`); audio/video
  duration via Gst `uridecodebin` (`:284-334`); notifications scale to
  256px (`notifications.ts:88`, `:127`).
- **Virtual paste** (`keyboard.ts:6`): `seat.create_virtual_device(KEYBOARD)`
  (`:12`); see §2 for the Shift+Insert timing.
- **Shell defaults**: `xargs xdg-open` (image/file/link), `nautilus -s $1`
  (reveal), `cut -c8-` (paste path without `file://`) — `actions.ts:184`,
  `:194`, `:204`, `:217`.
