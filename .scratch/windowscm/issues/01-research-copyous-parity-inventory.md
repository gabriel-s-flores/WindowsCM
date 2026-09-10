# Inventário de paridade Copyous

Type: research
Status: resolved

## Question

Auditar o código do Copyous (https://github.com/boerdereinar/copyous, branch main) e produzir o inventário canônico de paridade total: cada tipo, cada pref, cada atalho, cada ação default, schema DB, tags, temas — com evidência arquivo:linha.

Cobrir no mínimo:
- `src/lib/common/constants.ts` (ItemType, Tags + hex, HljsLanguages)
- `src/lib/misc/clipboard.ts` (pipeline Image>File>Text, Link/Character/Color/Code/Text, MD5 dedup, exclusões, incognito)
- `src/lib/database/gda.ts` + `resources/database/database.sql` (schema v2, clear/protect/deleteOldest)
- `src/lib/common/actions.ts` + prefs actions (model, matching, defaults, Default Actions por tipo)
- `src/prefs.ts` + `resources/schemas/*.gschema.xml` (lista completa General/Customization/Shortcuts/Actions)
- `src/lib/common/dbus.ts` (Toggle/Show/Hide/ClearHistory)
- `src/lib/ui/clipboardDialog.ts` + items/* + themes SCSS (layout, cards, cores dark/light/high-contrast)
- Dependências (GSound, Soup link preview, highlight.js, GdkPixbuf, VirtualInputDevice paste)

## Answer

Achados em `.scratch/windowscm/research/01-parity-inventory.md` (clone raso em `C:\Users\gabri\AppData\Local\Temp\opencode\copyous`, commit `fa103e3`; só código+schemas+SQL+README, cada claim com `copyous:arquivo:linha`). 8 seções, 440 linhas, ~28k chars, todas as áreas da Question cobertas.

Load-bearing para a spec WPF:
- Dados portáveis: 8 tipos (Text, Code, Image, File, Files, Link, Character, Color), 9 tags hex fixas, 1 tabela SQLite v2 `UNIQUE(type,content)` + `pinned/tag/datetime/metadata/title`.
- Classificação é regra pura (prefixo http + `GLib.uri_is_valid` → Link; `Intl.Segmenter` grapheme ≤ max-chars → Character; `Color.parse` 10 espaços CSS Color 4 → Color; `highlightAuto` slice 10k, `relevance/n>=3` → Code; senão Text). Pipeline Image > File > Text, dedup MD5, `prevClipboard` setado antes de `shouldSave` (incognito não vaza).
- Ações (`command/color/qrcode`, regex+tipos, stdin/stdout, timeout 30s, defaults por tipo) viram Command + JSON quase 1:1. DBus vira named pipe/CLI. Prefs (~70 chaves, 4 páginas) viram Settings + UI.
- Difícil: monitor global Win32 (formatos CF_*, donos que somem; exclusões por HWND/processo, não WM_CLASS), thumbnails (WIC/Shell providers + MediaFoundation), UI (diálogo modal H/V, cards, busca com Collator, 3 temas SCSS — maior custo), GSound (empacotar wavs próprios), parser de cor completo + glob→regex precisam de port fiel.
- Lacunas: `actionsGroup/actionSubMenuDialog/editDialog/qrCodeDialog/scrollContainer/layout/theme/compatibility/icons/utils/shortcutRow` não auditados linha a linha; direção do `select_order_by(datetime)` no Gda assumida DESC; limites (tamanho imagem, QR capacity, rate-limit notificações) não achados; divergências anotadas (8 grupos de atalhos na UI vs 7 no ticket; `CustomColorScheme.HighContrast` sem gschema; SpinRows 300×200 vs schema 250×170 inócuo).
- Desbloqueia: `04` (persistência), `05` (ações), `06` (grilling configs) — podem ser claimados agora.
