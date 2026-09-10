# Persistência + busca + histórico

Type: research
Status: resolved
Blocked by: 01

## Question

Portar o modelo de dados do Copyous para `Microsoft.Data.Sqlite` (ou EF Core Sqlite — justificar) com paridade total.

Definir:
- Schema portado (tabela `clipboard(id,type,content,pinned,tag,datetime,metadata,title)` + `UNIQUE(type,content)` + `clipboard_version`), tipos `Text/Code/Image/File/Files/Link/Character/Color`, `metadata` JSON por tipo, `title` editável
- Onde ficam bytes de imagem e thumbnails de link (`%LocalAppData%/WindowsCM/...`), limpeza ao deletar entry
- Semântica: dedup por conflito, `updateProperty`, `clear(manter pins+tags)` vs tudo, `deleteOldest(history-length + history-time)` protegendo pins/tags, ordenação `datetime DESC`
- Backends: só SQLite ou manter JSON/Memory como fallback/debug? Path default + override
- Busca: `Remember Search`, `Exclude Pinned/Tagged`, `Protect Pinned/Tagged`, `Sync Primary` (N/A Windows?), `Update Date on Copy`

Só começar quando `01` estiver `resolved` (precisa do inventário).

## Answer

Achados em `.scratch/windowscm/research/04-persistencia-busca.md` (só fontes primárias; 6 seções, DDL + SQL prontos para a spec).

Load-bearing:
- **`Microsoft.Data.Sqlite` direto** (EF Core rejeitado: tracker/migrations/rebuild + NativeAOT sem queries dinâmicas). `UNIQUE(type,content)` + `INTEGER 0/1` + `TEXT ISO8601` + `WAL` + `BackupDatabase` confirmados; `BOOLEAN`/`DATETIME` como nomes têm afinidade NUMERIC — usar `INTEGER`/`TEXT`.
- Upsert `ON CONFLICT(type,content) DO UPDATE` bumpeando `datetime` (nunca `INSERT OR REPLACE` — troca o `id`). `updateProperty` em type/content com UNIQUE violado retorna id conflitante (paridade Gda), sem edição parcial.
- `clear` = `DELETE WHERE NOT(pinned=1 OR tag IS NOT NULL)`; `deleteOldest` = além-de-N UNION mais-velhos-que-M, ambos com proteção. Índices `datetime DESC` + `(pinned,tag)`.
- `metadata` TEXT + `System.Text.Json` (options estática, `JsonSerializerContext` p/ AOT); JSON corrompido → `metadata=null`, nunca dropa entry.
- Imagens `%LocalAppData%\WindowsCM\images\<md5>.<ext>`, thumbs `cache\<md5>`; deleta arquivo só após commit + GC de órfãos no startup.
- Backends: **SQLite produção + Memory (`:memory:`) só p/ testes; JSON fora**. Override `WINDOWSCM_DBPATH` > settings > default.
- **`Sync Primary` = N/A permanente** (Win32 tem um clipboard só — trancado). `Remember Search` em settings de sessão, `Exclude-*` filtros, `Protect-*` no WHERE, `Update Date on Copy` = UPDATE no copy-from-history. Busca v1 `LIKE ... ESCAPE COLLATE NOCASE` + fallback `title`; sem `Cache=Shared` com WAL; async ADO.NET roda síncrono.
- Aberto p/ spec decidir: `STRICT` na DDL, formato `datetime` (`T...Z` vs driver), variante de fusão `track()`, `busy_timeout`.
