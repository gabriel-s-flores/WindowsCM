# Persistence + search + history

Type: research
Status: resolved
Blocked by: 01

## Question

Port the Copyous data model to `Microsoft.Data.Sqlite` (or EF Core Sqlite — justify) with full parity.

Define:
- Ported schema (table `clipboard(id,type,content,pinned,tag,datetime,metadata,title)` + `UNIQUE(type,content)` + `clipboard_version`), types `Text/Code/Image/File/Files/Link/Character/Color`, per-type `metadata` JSON, editable `title`
- Where image bytes and link thumbnails live (`%LocalAppData%/WindowsCM/...`), cleanup when deleting an entry
- Semantics: dedup on conflict, `updateProperty`, `clear(keep pins+tags)` vs everything, `deleteOldest(history-length + history-time)` protecting pins/tags, `datetime DESC` ordering
- Backends: SQLite only, or keep JSON/Memory as fallback/debug? Default path + override
- Search: `Remember Search`, `Exclude Pinned/Tagged`, `Protect Pinned/Tagged`, `Sync Primary` (N/A on Windows?), `Update Date on Copy`

Only start when `01` is `resolved` (it needs the inventory).

## Answer

Findings in `.scratch/windowscm/research/04-persistence-search.md` (primary sources only; 6 sections, DDL + SQL ready for the spec).

Load-bearing:
- **`Microsoft.Data.Sqlite` directly** (EF Core rejected: tracker/migrations/rebuild + NativeAOT without dynamic queries). `UNIQUE(type,content)` + `INTEGER 0/1` + `TEXT ISO8601` + `WAL` + `BackupDatabase` confirmed; `BOOLEAN`/`DATETIME` as names have NUMERIC affinity — use `INTEGER`/`TEXT`.
- Upsert `ON CONFLICT(type,content) DO UPDATE` bumping `datetime` (never `INSERT OR REPLACE` — it changes the `id`). `updateProperty` on type/content with a violated UNIQUE returns the conflicting id (Gda parity), no partial edit.
- `clear` = `DELETE WHERE NOT(pinned=1 OR tag IS NOT NULL)`; `deleteOldest` = beyond-N UNION older-than-M, both with protection. Indexes `datetime DESC` + `(pinned,tag)`.
- `metadata` TEXT + `System.Text.Json` (static options, `JsonSerializerContext` for AOT); corrupted JSON → `metadata=null`, never drops the entry.
- Images `%LocalAppData%\WindowsCM\images\<md5>.<ext>`, thumbs `cache\<md5>`; delete the file only after commit + GC of orphans at startup.
- Backends: **SQLite in production + Memory (`:memory:`) for tests only; JSON out**. Override `WINDOWSCM_DBPATH` > settings > default.
- **`Sync Primary` = permanently N/A** (Win32 has a single clipboard — locked). `Remember Search` in session settings, `Exclude-*` filters, `Protect-*` in the WHERE, `Update Date on Copy` = UPDATE on copy-from-history. v1 search `LIKE ... ESCAPE COLLATE NOCASE` + `title` fallback; no `Cache=Shared` with WAL; async ADO.NET runs synchronously.
- Open for the spec to decide: `STRICT` in the DDL, `datetime` format (`T...Z` vs driver), `track()` merge variant, `busy_timeout`.
