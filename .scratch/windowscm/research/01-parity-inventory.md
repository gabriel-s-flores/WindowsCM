# 01 — Inventário de paridade Copyous (fontes primárias)

Fonte: clone raso de `https://github.com/boerdereinar/copyous` em
`C:\Users\gabri\AppData\Local\Temp\opencode\copyous`, commit `fa103e3`
(`fix: prevent grab race condition during close animation (#133)`).
Auditoria restrita a código + schemas + SQL + README. Citações no formato
`copyous:<arquivo>:<linha>` referem-se a esse checkout.

## 1. Tipos, tags, cores, paths, highlight.js (`constants.ts`)

- `ItemType` = Text, Code, Image, File, Files, Link, Character, Color
  (`copyous:src/lib/common/constants.ts:9`, valores em `:10-18`; lista
  `ItemTypes` em `:22-31`).
- `Tag` = blue, teal, green, yellow, orange, red, pink, purple, slate
  (`copyous:src/lib/common/constants.ts:33`, valores em `:34-43`; lista
  `Tags` em `:47-57`).
- Hex das tags (SCSS): blue `#3584e4`, teal `#2190a4`, green `#3a944a`,
  yellow `#c88800`, orange `#ed5b00`, red `#e62d42`, pink `#d56199`,
  purple `#9c3cbe`, slate `#6f8396`
  (`copyous:resources/css/themes/default/_copyous-colors.scss:1`; valores
  em `:1-9`). Cor de texto insensível do card deriva de variante/contraste
  (`copyous:resources/css/themes/default/_copyous-colors.scss:11`).
- `ActiveState` = None 0, Focus 1, Hover 2, FocusHover 3, Active 4
  (`copyous:src/lib/common/constants.ts:59`).
- `DefaultColors` (fallback do tema custom, `[dark, light]`):
  `custom-bg-color` rgb(54,54,58)/rgb(250,250,251),
  `custom-fg-color` rgb(255,255,255)/rgb(34,34,38),
  `custom-card-bg-color` rgb(71,71,76)/rgb(255,255,255),
  `custom-search-bg-color` rgb(71,71,76)/rgb(255,255,255)
  (`copyous:src/lib/common/constants.ts:69`).
- Paths (XDG + uuid): cache `getCachePath`
  (`copyous:src/lib/common/constants.ts:286`), data `getDataPath` (`:290`),
  `images/` filho de data `getImagesPath` (`:294`), config `getConfigPath`
  (`:298`), `actions.json` filho de config `getActionsConfigPath` (`:302`).
  DB default: Json → `<data>/clipboard.json`, senão `<data>/clipboard.db`
  (`copyous:src/lib/common/constants.ts:306`).
- `UserAgent` = `Mozilla/5.0 (compatible; CopyousBot/1.0; +https://github.com/boerdereinar/copyous)`
  (`copyous:src/lib/common/constants.ts:76`).
- highlight.js 11.11.1: 3 CDNs (cdnjs, jsdelivr, unpkg)
  (`copyous:src/lib/common/constants.ts:78`; `package.json:24` fixa
  `"highlight.js": "11.11.1"` em `copyous:package.json:24`);
  `HljsUrls` = `<cdn>/highlight.min.js`
  (`copyous:src/lib/common/constants.ts:84`); SHA-512
  `f35f2463…675f1` (`copyous:src/lib/common/constants.ts:86`).
- `HljsLanguages`: 192 linhas `[id, Nome, sha512]`
  (`copyous:src/lib/common/constants.ts:91`; primeira em `:92`, última
  `zephir` em `:283`). Resolução: `ext.dir/highlight.min.js` (sistema) senão
  `<data>/highlight.min.js` (`copyous:src/lib/common/constants.ts:312`);
  linguagens: `ext.dir/languages/<id>.min.js` senão `<data>/languages/<id>.min.js`
  (`copyous:src/lib/common/constants.ts:333`); URLs por idioma
  `<cdn>/languages/<id>.min.js`
  (`copyous:src/lib/common/constants.ts:343`).

## 2. Pipeline do clipboard (`misc/clipboard.ts`)

- Mimetypes aceitos: Text `text/plain;charset=utf-8, UTF8_STRING, text/plain, STRING`;
  Image `image/png, image/jxl, image/webp, image/avif, image/jpeg`;
  File `x-special/gnome-copied-files, text/uri-list`; sensível
  `x-kde-passwordManagerHint` (`copyous:src/lib/misc/clipboard.ts:25`).
- `ContentType` Text 0, Image 1, File 2
  (`copyous:src/lib/misc/clipboard.ts:32`).
- Ordem de sondagem em `getContent`: **Image > File > Text**
  (`copyous:src/lib/misc/clipboard.ts:306`, `:319`, `:340`).
- Texto vazio/whitespace-only é descartado (`:344`); imagem de 0 bytes → null
  (`:309-315`); arquivo vazio → null (`:334-336`). Arquivo: primeira linha
  `copy|cut` (case-insensitive) define operação, resto = paths; sem operação
  → `copy` (`copyous:src/lib/misc/clipboard.ts:324`).
- Dedup MD5: texto `MD5(text)`, imagem `MD5(bytes)`, arquivo
  `MD5(paths sem file:// unidos por \n)`
  (`copyous:src/lib/misc/clipboard.ts:45`). `prevClipboard=[type, checksum]`
  ignora repetição do próprio Copyous (`:256-260`); guarda especial: perda de
  ownership de arquivo no Nautilus (File→Text com mesmo checksum) não atualiza
  (`:264-266`); `prevClipboard` é setado **antes** de `shouldSave` para que
  cópia em incognito não vaze depois (`:269-274`).
- `shouldSave`: falso se mimetype sensível (`:225`), se `wm_class` da janela
  focada está em `wmclass-exclusions` (`:230-234`), ou se `incognito`
  (`:236`).
- Classificação `convertContent` (só entra o que passou em `shouldSave`):
  1. Link: `trimmed.startsWith('http') && GLib.uri_is_valid(...)`
     (`copyous:src/lib/misc/clipboard.ts:359`).
  2. Character: `!hasMoreGraphemes(trimmed, maxCharacters)` com segmentação
     `Intl.Segmenter` grapheme (`:58-62`, `:368-371`); limite vem de
     `get_child('character-item').get_int('max-characters')` (`:368`).
  3. Color: `Color.parse(trimmed)` (`:374-376`; gramática em `color.ts:160-174`,
     named+hex+rgb+hsl+hwb+linear-rgb+xyz+lab+lch+oklab+oklch em `:329-343`).
  4. Code: `slice(0,10000)` (`:379`), `n=max(1,len/100)` (`:380`),
     `highlightAuto` e `relevance/n >= 3` (`:381-382`); metadata
     `{language:{id,name}}` com capitalização ajustada (`:386-388`).
  5. Text fallback (`:393`).
  6. Image: cria `images/` se preciso, nome `<md5>.<ext-do-mimetype>`, só grava
     se não existir, conteúdo = `file://` URI
     (`copyous:src/lib/misc/clipboard.ts:397`).
  7. File: 1 path → `File`, N → `Files` (`\n`-joined), metadata `{operation}`
     (`:421-428`).
- Copiar de volta (`handleEntry`): Text/Code/Link/Character/Color → texto puro
  (`:194-199`); Image → lê URI, `content_type_guess`, MD5, `set_content`
  (`:200-214`); File/Files → `split('\n')`, operação forçada `copy` (`:215-219`).
  `sync-primary` duplica texto no PRIMARY (`:116-118`); `update-date-on-copy`
  refresca `datetime` ao copiar do histórico (`:189-191`).
- Colar: `copyContent` + timeout **250 ms** (`:141`), depois Shift+Insert, ou
  **Ctrl+Shift+Insert em terminal** (`purpose === TERMINAL`, `:143-155`);
  teclado via `Clutter.VirtualInputDevice` (`keyboard.ts:12`, press/release em
  `:36-42`, purpose tracking em `:14-20`).

## 3. Banco de dados (`gda.ts`, `database.sql`, `json.ts`, `memory.ts`)

- Schema v2: `clipboard_version(id=1, version=2)`
  (`copyous:resources/database/database.sql:59`; insert `:65`;
  `DATABASE_VERSION = 2` em `copyous:src/lib/database/gda.ts:13` e
  `copyous:src/lib/database/json.ts:14`).
- Tabela `clipboard(id INTEGER PK AUTOINCREMENT UNIQUE, type TEXT NOT NULL,
  content TEXT NOT NULL, pinned BOOLEAN NOT NULL, tag TEXT NULL,
  datetime TIMESTAMP NOT NULL, metadata TEXT NULL, title TEXT NULL,
  UNIQUE(type, content))` (`copyous:resources/database/database.sql:46`;
  UNIQUE em `:55`; migração v0 cria sem `title`
  em `copyous:src/lib/database/gda.ts:307`, v1 `ALTER TABLE ADD COLUMN title`
  em `:325`).
- `database.sql` inclui ainda trigger `decrease_time` e massa de teste
  (tags/cores/emoji/links/arquivos/código/texto/títulos) — **só fixture de
  teste**, não schema de produção
  (`copyous:resources/database/database.sql:67`).
- `ClipboardEntry` GObject: id(readonly), type, content, pinned, tag,
  datetime, metadata, title (`copyous:src/lib/database/database.ts:57`).
  `Metadata` = `CodeMetadata{language{id,name}|null}` | `FileMetadata{operation
  copy|cut}` | `LinkMetadata{title,description,image}`
  (`copyous:src/lib/database/database.ts:11`; `FileOperation` em `:31`).
- Interface `Database`: init/clear/close/entries/selectConflict/insert/
  updateProperty/delete/deleteOldest
  (`copyous:src/lib/database/database.ts:112`).
- Ordenação: `entries()` ordena por `datetime` (Gda `select_order_by(datetime)`
  em `copyous:src/lib/database/gda.ts:416`; Memory `sort(b-a)` =
  **mais-recente-primeiro** em `copyous:src/lib/database/memory.ts:49`).
- `clear(history)`: `KeepAll` → nada; `KeepPinnedAndTagged` → apaga só
  `NOT(pinned OR tag IS NOT NULL)`; `Clear` → tudo
  (`copyous:src/lib/database/memory.ts:20`; predicado SQL em
  `copyous:src/lib/database/gda.ts:715`).
- Proteção é por **sobrevivência**, não por bloqueio de UI separada:
  `deleteOldest(offset=N, olderThanMinutes=M)` apaga não-protegidos além de N
  **UNION** não-protegidos mais velhos que M
  (`copyous:src/lib/database/gda.ts:645`; Memory em
  `copyous:src/lib/database/memory.ts:111`).
- `insert` falha em conflito (UNIQUE) retornando null
  (`copyous:src/lib/database/memory.ts:59`; Gda `:525`);
  `updateProperty` em `type/content` conflitante retorna o **id do
  conflitante** (`copyous:src/lib/database/gda.ts:599`); Memory só suporta
  update de `content` (`copyous:src/lib/database/memory.ts:80`).
- Backends e default: ordem Default = `in-memory-database`(deprecated) →
  `<nome>.db` existe → `<nome>.json` existe → tenta SQLite → tenta JSON →
  memory (`copyous:src/lib/database/entryTracker.ts:43`; espelho na UI em
  `copyous:src/lib/preferences/general/historySettings.ts:194`).
  `getFile()` honra `DEBUG_COPYOUS_DBPATH`, senão `database-location` ou
  default (`copyous:src/lib/database/entryTracker.ts:99`).
  Gda ausente → warning com ação `disable-gda-warning`
  (`copyous:src/lib/database/entryTracker.ts:128`).
- Tracker: `selectConflict` antes de inserir; se já tracked, só bumpa
  `datetime` e retorna null (reordena sem duplicar)
  (`copyous:src/lib/database/entryTracker.ts:198`); `deleteOldest` roda a cada
  insert e usa `history-length`(N)/`history-time`(M) (`:219-245`);
  `checkOldest` varre em memória (`:225-238`); `track()` assina
  content/pinned/tag/datetime/metadata/title e funde conflito de edição
  (`:247-270`); ao deletar Image apaga o arquivo, ao deletar Link apaga o
  thumbnail em cache (`:272-303`).
- JSON backend: mesma semântica Memory + persistência debounced **1000 ms**
  (`copyous:src/lib/database/json.ts:114`), ISO-8601 UTC
  (leitura `:61`, escrita `:134`), `title || undefined` (`:136`), cria dir
  (`:141-144`). `json.sql` é o exportador SQLite→JSON
  (`version:2`, `strftime('%FT%T.000000Z', datetime,'utc')`)
  (`copyous:resources/database/json.sql:1`).
- Limpeza periódica: `extension.ts` agenda `deleteOldest` a cada **60 s** quando
  `history-time > 0`, adiando com diálogo aberto
  (`copyous:src/extension.ts:272`); Gda escapa `\\`→`\` por bug do libgda
  (`copyous:src/lib/database/gda.ts:227`).

## 4. Ações (`actions.ts` + prefs)

- Modelo: `ActionConfig{actions:(Action|ActionSubmenu)[], defaults?: Partial<Record<ItemType,string>>}`
  (`copyous:src/lib/common/actions.ts:13`); submenu `{name, actions}`
  (`:18`); ação `{kind, id, name, pattern?, types?, output, shortcut?}`
  (`:31`); `CommandAction{command}` (`:41`), `ColorAction{space,
  types:[Color], output:copy|paste}` (`:46`), `QrCodeAction{output:ignore}`
  (`:53`); `ActionOutput` ignore/copy/paste (`:23`).
- Matching: `types` vazio/nulo = todos; `pattern` = `new RegExp(pattern)` com
  `test` (`testAction`, `copyous:src/lib/common/actions.ts:83`) e `match`
  retornando grupos (`matchAction`, `:102`); regex inválida = sem match (`:91-93`,
  `:110-112`). `isDefaultAction`/`findDefaultAction` resolvem por
  `defaults[entry.type]` (`:144-157`).
- Execução (`actionMenu.ts`): comando roda `sh -c <command> _ <grupos...>`
  com **stdin = conteúdo** (`copyous:src/lib/ui/components/actionMenu.ts:217`;
  documentado na UI em `copyous:src/lib/preferences/actions/actionDialog.ts:398`);
  timeout **30 s** (`copyous:src/lib/ui/components/actionMenu.ts:199`);
  stdout `trim`, vazio = ignora (`:222-224`); `copy|paste` emitem sinais
  (`:226-232`); cor converte via `Color.toColor(space)` (`:244-259`); QR abre
  diálogo (`:261-266`); item default ganha ornamento DOT (`:99-106`); config
  recarrega por FileMonitor (`:131-137`, também em
  `copyous:src/lib/misc/shortcuts.ts:99`).
- Defaults built-in (`defaultConfig`, `copyous:src/lib/common/actions.ts:163`):
  submenu `Open`: `open-with-default` (`xargs xdg-open`, Image+File, `:184`),
  `open-with-files` (`nautilus -s $1`, pattern `^(.*)`, Image+File+Files, `:194`),
  `open-with-browser` (`xargs xdg-open`, Link, `:204`); `paste-as-path`
  (`cut -c8-`, output paste, Image+File+Files, `:217`); submenu `Convert`:
  rgb `^(?!rgb)`, hex `^(?!#)`, hsl `^(?!hsl)`, oklch `^(?!oklch)` (hwb/linear/
  xyz/lab/lch/oklab comentados, `:227-239`), todos output paste shortcut [];
  `qrcode` (Text+Code+Link+Character+Color, output ignore, shortcut
  `<Control>q`, `:241-249`). `defaults`: File→`paste-as-path`,
  Files→`paste-as-path`, Link→`open-with-browser` (`:251-255`).
- CRUD na prefs (`actionsPage.ts`): lista (`ActionsGroup`), página Default
  Actions, Restore (merge built-ins faltantes, preserva customs e
  `config1.defaults`) e Reset (volta a `defaultConfig`), ambos com backup
  (`copyous:src/lib/preferences/actions/actionsPage.ts:114`; merge em
  `copyous:src/lib/common/actions.ts:345`, badge `countDifference` em `:329`).
  Formulários (`actionDialog.ts`): Command exige nome+comando
  (`:496-498`), outputs Ignore/Copy/Paste (`:454-464`); Color exige nome, 10
  espaços, outputs Copy/Paste (`:529-555`, `:585-587`); QRCode exige nome,
  output fixo ignore (`:595-650`); ids novos = `uuid_string_random`
  (`:476`, `:566`, `:630`); `types` vazio = todos (`:488`, `:641`).
- Página Default Actions tem 1 linha por **tipo (8)** com filtro por
  aplicabilidade e opção None (`copyous:src/lib/preferences/actions/actionDefaults.ts:99`;
  linhas por tipo em `:132-167`; `validType` vazio=todos em `:18`).
- Persistência: `actions.json` (tab-indentado, flag `backup`)
  (`copyous:src/lib/common/actions.ts:294`; load com override
  `DEBUG_COPYOUS_ACTIONS` em `:264-268`).

## 5. Preferências (`prefs.ts` + `gschema.xml` + `preferences/**`)

Páginas (`copyous:src/prefs.ts:57`): General (History, Feedback, Behavior,
AppExclusions, Dependencies, Locations em `:80-90`), Customization (Profiles,
Dialog, Item, Header, Items, Theme em `:100-107`), Shortcuts (8 grupos em
`:117-124` — Dialog, Item, ItemActivation, PopupMenu, Navigation, Search,
SearchNavigation, SearchScroll), Actions (`:127-129`).

### General
- History (`historySettings.ts:48` + gschema `:113-139`): `database-backend`
  enum default/memory/sqlite/json, default `default` (`:118`);
  `database-location` string default `''` = `<data>/clipboard.{db,json}`
  (`:122`); `clipboard-history` clear/keep-pinned-and-tagged(default)/keep-all
  (`:126`); `history-length` int 10–500 default 50 (`:130`);
  `history-time` int 0–1440 default 0 = sem limite (`:135`);
  `in-memory-database` bool default false **deprecated** (`:114`). UI: combo
  Memory/SQLite(recommended)/JSON (`:69-106`), linha location com file-chooser
  (`*.db`/`*.json`, `:267-285`), spins (`:142-156`), gating por Gda
  (`:194-228`).
- Feedback (`feedbackSettings.ts:213` + gschema `:175-215`):
  `indicator-display` hidden/icon-only(default)/content-only/icon+content
  (`:176`); `wiggle-indicator` default true (`:188`, sensível só com ícone em
  `:274-280`); `send-notification` default false (`:192`);
  `sound` none/click/hum/string/swing/message/message-new-instant/bell/
  dialog-warning, default none (`:196`); `volume` double −20…+20 dB default 0.0
  (`:211`); `show-indicator`/`show-content-indicator` **deprecated**, migrados
  para `indicator-display` (`settings.ts:544`; gschema `:180-187`). UI som:
  radio + slider dB com preview (`feedbackSettings.ts:122`; `:200-204`).
- Behavior (`behaviorSettings.ts:11` + gschema `:141-173`): `remember-search`
  false (`:141`), `exclude-pinned` false (`:146`), `exclude-tagged` false
  (`:150`), `protect-pinned` true (`:154`), `protect-tagged` true (`:158`),
  `sync-primary` false (`:166`), `update-date-on-copy` true (`:170`);
  `paste-on-copy` **deprecated** → migra invertido para `swap-copy-shortcut`
  (`settings.ts:544`; gschema `:162`).
- Exclusions (`appExclusionSettings.ts:313` + gschema `:217-221`):
  `wmclass-exclusions` strv default [] (`:218`); UI com catálogo de apps +
  entrada manual de WM_CLASS (`:25-210`, página em `:290-311`).
- Dependencies/Locations: `disable-gda-warning`/`disable-hljs-dialog` bool
  false (`gschema:104`; `:108`); página hljs com install/uninstall por idioma
  (`dependenciesSettings.ts:273`); Locations só abre Data/Config/Cache
  (`locationsGroup.ts:10`).

### Customization
- Profiles: Default vs Compact (valores literais em
  `copyous:src/lib/preferences/customization/profiles.ts:164`):
  Default(show-at-pointer F, horizontal, top/fill, size 500, auto-hide F,
  250×170, dynamic F, header V, controls visible, file preview-or-info, link
  vertical); Compact(pointer T, vertical, fill/left, 500, auto-hide T, 300×100,
  dynamic T, header hidden, controls on-hover, file info-only, link
  horizontal).
- Dialog (`dialogCustomization.ts:13` + gschema `:223-287`): `show-at-pointer`
  F (`:224`), `show-at-cursor` F (`:228`), `clipboard-orientation`
  horizontal (`:232`), `clipboard-position-vertical` top (`:236`),
  `clipboard-position-horizontal` fill (`:245`), `clipboard-size` 200–10000
  default 500 (`:254`), margens 0–10000 default 6 (`:260-279`),
  `auto-hide-search` F (`:280`), `show-scrollbar` T (`:284`). Posição vertical
  aceita alias left→top/right→bottom (gschema `:240-243`, idem horizontal
  `:249-252`).
- Item (`itemCustomization.ts:13` + gschema `:289-308`): `item-width`
  200–1000 default **250** (`:290`), `item-height` 50–1000 default **170**
  (`:295`), `dynamic-item-height` F — só vertical (`:300`, gating em
  `:67-71`), `tab-width` 1–8 default 4 (`:304`).
- Header (`headerCustomization.ts:13` + gschema `:310-322`): `show-header` T
  (`:311`), `header-controls-visibility` visible/on-hover/hidden default
  visible (`:315`), `show-item-title` T (`:319`).
- Items por tipo (child schemas):
  text (`show-text-info` F, `text-count-mode` characters/words/lines default
  characters; `copyous:resources/schemas/...gschema.xml:386`);
  code (syntax-highlighting T, show-line-numbers T, show-code-info F,
  text-count-mode characters; `:397`);
  image (show-image-info F, background-size cover/contain default cover; `:416`);
  file (visibility preview/file-info/**preview-or-file-info(default)**/
  preview-and-info/hidden `:427`; types flags text|image|thumbnail default
  todos `:432`; exclusion glob [] `:435`; background cover `:439`; hl T `:443`;
  lines T `:447`);
  link (preview T `:454`, preview-image T `:458`, image-bg **contain** `:462`,
  orientation **vertical** `:466`, exclusion regex [] `:470`);
  character (max-characters 1–4 default **1** `:476`, show-unicode F `:480`).
- Theme (child `theme`, `:488`): `theme` default/yaru/custom default default
  (`:489`); `color-scheme` system/dark/light/high-contrast default system
  (`:493`); `custom-color-scheme` dark/light default dark (`:497`) — notar que
  `settings.ts:349` prevê `HighContrast:2` sem correspondente no schema;
  4 cores custom string default `''` com fallback `DefaultColors`
  (`:501-516`; lógica em `themeCustomization.ts:46`).

### Shortcuts (8 grupos na UI; schema só persiste parte)
Persistidos (`gschema:335-383`): `open-clipboard-dialog-shortcut`
`[<Super><Shift>v]` (`:336`), `toggle-incognito-mode-shortcut`
`[<Super><Control><Shift>v]` (`:340`),
`open-clipboard-dialog-behavior` toggle/open-or-select-next default toggle
(`:344`), `pin-item-shortcut [<Control>s]` (`:349`),
`delete-item-shortcut [Delete]` (`:353`), `edit-item-shortcut [<Control>e]`
(`:357`), `edit-title-shortcut [<Control>t]` (`:361`),
`open-menu-shortcut [<Control>a]` (`:365`),
`middle-click-action` none/**pin(default)**/delete (`:370`),
`swap-copy-shortcut` F (`:375`), `swap-scroll-shortcut` F (`:380`).
Hardcoded (sem chave): ativação Paste `Return/space`, Copy
`<Shift>Return/space`, default `<Ctrl>Return/space` + swap
(`itemShortcuts.ts:61`; linhas `:72-89`); Navigation Tab/arrows/Home/End,
`Ctrl+0..9`, `Ctrl+F` (`navigationShortcuts.ts:9`); Popup `0..9`
(`popupMenuShortcuts.ts:9`); Search `Alt` pinned, `Back` limpa tag/tipo,
`Return` primeiro item (`searchShortcuts.ts:46`); SearchNav
`Ctrl+Tab`/`Ctrl+Shift+Tab`, `` Ctrl+` ``/`Ctrl+Shift+` ``, `Ctrl+Shift+0..9`
(`:57-68`); SearchScroll scroll=ciclo tipo vs `Ctrl+scroll`=ciclo tag
(+swap) (`searchEntry.ts:578` + `searchShortcuts.ts:70`).
README resume os principais (`copyous:README.md:74`; tabela `:77-89`).

## 6. D-Bus + extension (`dbus.ts`, `extension.ts`)

- Interface XML `org.gnome.Shell.Extensions.Copyous`: `Toggle`, `Show`,
  `Hide`, `ClearHistory(in b all)` (`copyous:src/lib/common/dbus.ts:7`).
- Nome `org.gnome.Shell.Extensions.Copyous` (session)
  (`copyous:src/lib/common/dbus.ts:48`), objeto
  `/org/gnome/Shell/Extensions/Copyous` (`:84-87`). Documentado no README
  (`copyous:README.md:91`; tabela `:95-100`; exemplos gdbus `:102-114`).
- `ClearHistory(true)` → `ClipboardHistory.Clear`, `false` →
  `KeepPinnedAndTagged` (`copyous:src/lib/common/dbus.ts:72`); sinais GObject
  toggle/show/hide/clear-history (`:27-35`).
- Auto-limpeza: `ConfirmedLogout/Reboot/Shutdown` (SessionManager) +
  `PrepareForShutdown` (login1 system) → `emit('clear-history', -1)`
  (`copyous:src/lib/common/dbus.ts:89`; `:100`, `:110`, `:120`); extension
  traduz `-1` → pref atual `clipboard-history`
  (`copyous:src/extension.ts:105`); wiring toggle/show/hide em `:98-108`;
  `destroy` unexport + unsubscribe (`copyous:src/lib/common/dbus.ts:77`).
- Ciclo extension: settings+migrate → hljs → tema → dialog+indicator → dbus →
  feedback → shortcuts → tracker+DB → clipboard
  (`copyous:src/extension.ts:49`; handlers de copy/paste/clear em `:64-108`;
  clipboard→UI/som/notificação em `:145-169`).

## 7. UI do diálogo (`clipboardDialog.ts`, `items/*`, `components/*`, `css/**`)

- Estrutura: `St.BoxLayout` vertical `.clipboard-dialog.horizontal|.vertical`
  com Header / ScrollView / Footer + popup menu
  (`copyous:src/lib/ui/clipboardDialog.ts:293`; filhos em `:310-341`).
  Header: settings + incognito + search central + Clear
  (`:86-200`); Footer (só vertical, `:689-690`): settings + incognito + Clear
  (`:208-243`). Animação 150 ms (`:37`), modal `SYSTEM_MODAL` (`:428`),
  GNOME≥49 escala 0.96 EASE_OUT_QUAD (`:464-470`).
- Layout: `clipboard-orientation` alterna classe e trava width(size) vs
  height(size) (`:674-686`, default 500); posição `(pos+1)%4` (`:670-671`);
  `show-at-pointer` centraliza + FitConstraint na mira
  (`:664-668`, `:710-725`); `show-at-cursor` usa IBus com offset +4px
  (`:715-716`, captura em `:702-708`); margens via inline
  `margin: T R B L` (`:693-700`); header colapsável por `auto-hide-search`
  (`:161-178`); clique/toque fora fecha (`:812-832`); Esc fecha (`:741-744`).
- Busca: `SearchQuery{query,pinned,tag,type}` + flags exclude
  (`searchEntry.ts:48`); substring locale-insensível via `Intl.Collator`
  sensitivity base (`:18-26`); otimização Same/LessStrict/MoreStrict
  (`:84-92`); fallback para `entry.title` (`:91`); popup com All+8 tipos
  (mnemônicos) e Tags (`:131-163`); middle-click limpa tag+tipo (`:303-310`);
  `remember-search=F` limpa ao desmapear (`:598-607`); `exclude-pinned/tagged`
  re-disparam (`:370-376`); atalhos de teclado do diálogo em
  `clipboardDialog.ts:737`.
- Scroll: horizontal = hscroll auto/vscroll never e vice-versa
  (`clipboardScrollView.ts:110`); `show-scrollbar=F` zera ambos (`:113-115`);
  fade 12px (`:36-44`); wheel avança `item+spacing` com ease 150 ms
  (`:180-208`); Home/End (`:141-159`).
- Cards (`clipboardItem.ts:32`): `St.Button` ONE|THREE (+TWO se middle-action,
  `:164-171`); tamanho = prefs (`item-width/height`, `:142-145`); altura
  dinâmica só vertical com `max-height` (`:147-154`); fundo com furo GLSL para
  botões do header (`HoleEffect`, `:309-395`); bindings
  pinned/datetime/tag/title (`:80-93`); proteção: delete bloqueado se
  protegido, Shift = force (`:190-198`, Delete com Shift em `:260-264`);
  clique esq ativa / Shift ativa-alt / Ctrl ação-default / meio pin|delete /
  direito menu (`:200-229`); teclas pin/delete/edit/edit-title/menu/ação
  (`:241-290`); swap-copy inverte activate/activate-shift
  (`clipboardDialog.ts:603`).
- Header do item (`clipboardItemHeader.ts:84`): ícone + título (ellipsis) +
  tempo relativo `formatTimeSpan` (`:45-47`) + pin/menu/delete/tag; título
  editável por duplo-clique com Entry (Return salva, Esc cancela, focus-out
  salva, vazio volta ao default, custom oculta o tempo)
  (`:143-171`, `:358-433`); controles visible/on-hover(active)/hidden
  (`:435-454`); delete some se protegido (`:445-448`); classe CSS `tag`
  (`:320`).
- Títulos default por tipo: Text (`textItem.ts:24`), Code (`codeItem.ts:23`),
  Image (`imageItem.ts:38`), File (`fileItem.ts:52`), Files
  (`filesItem.ts:140`), Link (`linkItem.ts:303`), Char **"Char"**
  (`characterItem.ts:22`), Color (`colorItem.ts:71`).
- Tags UI: botões `.tag-button.<tag>` com miolo (círculo)
  (`tagsItem.ts:11`); caixa rolável paginada com setas (`:42-97`); seleção
  exclusiva com toggle-off (`:195-208`).
- Cores/temas: `codeLabel.ts` mapeia classes `hljs-*` para paleta Adwaita
  Dark (`:84-141`) / Light (`:143-200`) via `applyTheme` (`:215-233`); com
  números de linha (`:395-401`). SCSS: variantes `default/dark`
  (`dark.scss:1`), `light` (`light.scss:1`), `high-contrast`
  (`high-contrast.scss:1`) + espelhos `yaru/`; widgets
  `_dialog.scss:1` (lista `.clipboard-item-list`, `:81-146`),
  `_clipboard-item.scss:22` (`%card`, header `:38`, content `:143`),
  `_search-entry.scss:1`, `_content-preview.scss:1`, `_popupmenu.scss:19`
  (tags `.tag-button` em `:83`), `_indicator.scss`, `_content-info.scss:1`.
- Indicador: modos hidden/icon/content/both
  (`indicator.ts:155`); conteúdo = 1ª linha (text/code/link/char), basename
  (file), "N Files" (files), swatch 32×32 (color), thumb (image)
  (`:181-213`); wiggle 2px/65ms×3 (`:175-179`); menu incognito/clear/settings
  (`:102-107`); clique esq abre diálogo, meio abre prefs (`:248-260`).
- Screenshot: `resources/images/screenshot.png` referenciado no README
  (`copyous:README.md:6`); features README (`:9-14`).

## 8. Dependências

- **GSound** (`sound.ts:1`): enum none/click/hum/string/swing/message/
  message-new-instant/bell/dialog-warning (`:10`);
  ogg GNOME em `<datadir>/sounds/gnome/default/alerts/<nome>.ogg` (`:54-65`),
  xdg = `null` (`:68-71`); play com `ATTR_MEDIA_FILENAME` (GNOME) ou
  `ATTR_EVENT_ID` (xdg) + `ATTR_CANBERRA_VOLUME` em dB string (`:90-108`);
  volume vem da pref (`:85-86`); `tryCreateSoundManager` retorna null se
  `gi://GSound` ausente (`:28-35`); guia de install por distro no diálogo
  (`dependencies.ts:354`; strings Fedora/Arch/Ubuntu/openSUSE em `:360-364`).
- **Soup link preview** (`link.ts:1`): `Session{user_agent: UserAgent,
  idle_timeout: 5}` (`:31`, `:186`); Accept html (`:34`) e imagem (`:189-192`);
  só `text/html` vira metadata (`:68`), `image/*` direto vira `{image:url}`
  com cache (`:42-65`); meta via regex `property|name` + reverso (`:86-90`);
  title `title|og:title|twitter:title|<title>` (`:93-98`), description idem
  (`:101`), imagem `image|og:image*|twitter:image` + `parse_relative`
  (`:104-118`); entidades HTML decodificadas (`:134-158`); cache
  `MD5(url)` em `getCachePath` (`:160-168`), hit pula download (`:182-183`).
  UI Link: respeita `show-link-preview(-image)`, bg, orientação e regex de
  exclusão (`linkItem.ts:335`; `:347-365`).
- **highlight.js**: ver §1 + `dependencies.ts:234` (download com verificação
  SHA-512 e fallback entre CDNs, `:284-299`; por idioma `:301-324`; já-instalado
  aborta `:242-245`); `extension.ts:172` importa dinamicamente e observa o
  arquivo para auto-load (`:193-207`); idiomas registrados/desregistrados por
  FileMonitor de diretório (`:209-253`); fallback sem hljs = texto escapado
  (`codeLabel.ts:382`); prefs exibem `HljsDialog` na 1ª falta salvo
  `disable-hljs-dialog` (`dependencies.ts:462`).
- **GdkPixbuf thumbnails** (`contentPreview.ts:4`): `get_file_info` para ratio
  (`:63`) e fallback `missing-image` (`:84-95`); `tryGetThumbnail` varre
  `~/.cache/thumbnails/*/<md5(uri)>.png` (`:202-229`); `getFileType`:
  diretório → Directory; thumbnail primeiro; `image/*` antes de `text/plain`
  (SVG!) depois audio/video/text (`:264-286`); texto = primeiros **4096 bytes**
  (`:189-195`); `ImageInfo` com dimensões (`contentInfo.ts:275`); duração
  áudio/vídeo via Gst `uridecodebin` (`:284-334`); notificações escalam para
  256px (`notifications.ts:88`, `:127`).
- **Paste virtual** (`keyboard.ts:6`): `seat.create_virtual_device(KEYBOARD)`
  (`:12`); ver §2 para timing Shift+Insert.
- **Shell defaults**: `xargs xdg-open` (imagem/arquivo/link), `nautilus -s $1`
  (revelar), `cut -c8-` (colar path sem `file://`) — `actions.ts:184`,
  `:194`, `:204`, `:217`.
