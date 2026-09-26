# 04 — Persistence + search + history (primary sources)

Base ported from `01-parity-inventory.md` §§3 (DB) and 5 (Behavior prefs) — no re-audit
of Copyous. All API claims below cite a primary source (Microsoft Learn .NET /
SQLite.org / Win32 docs). DDL + SQL ready for the spec.

## 1. `Microsoft.Data.Sqlite` vs `EF Core Sqlite` — recommendation: `Microsoft.Data.Sqlite`

**Recommendation: `Microsoft.Data.Sqlite` directly (ADO.NET), without EF Core.** Rationale:

- The EF Core provider for SQLite is built on top of `Microsoft.Data.Sqlite`
  ([overview](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)), so
  using the base layer removes the change tracker, the LINQ pipeline and migrations without
  losing anything the port needs (the port is fixed SQL + hand-versioned DDL).
- Direct SQL control required by parity: `UNIQUE(type,content)` + upsert with a
  conflict target, `DELETE ... WHERE NOT(pinned OR tag IS NOT NULL)`, `deleteOldest`
  with UNION/LIMIT and `ORDER BY datetime DESC` need literal parameterized SQL;
  in EF this would become dynamic LINQ + an escape-hatch `Sql()` — no gain.
- Migrations: the SQLite engine does not support several schema operations; the EF provider
  does a rebuild (create new table, copy, drop, rename) and throws `NotSupportedException`
  for artifacts outside the model
  ([operations table + workaround](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)).
  Copyous versions the schema by hand (`DATABASE_VERSION=2`, `ALTER TABLE ADD COLUMN title`
  in the v0→v1 migration — 01§3); porting that is ~15 lines of `CREATE TABLE IF NOT EXISTS` /
  `ALTER TABLE` + `clipboard_version`, versus bringing in the whole migrations package
  (which also creates the `__EFMigrationsLock` table and requires manual cleanup after a kill
  during a migration — [same page](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)).
- JSON: `metadata` is an opaque TEXT column with (de)serialization in `System.Text.Json`
  (§3). SQLite ≥3.38 already ships the JSON functions built in by default
  ([compiling in JSON support](https://www.sqlite.org/json1.html)), so validation
  (`json_valid`) exists in the engine without EF. There is no need for EF's JSON mapping
  (the provider's dedicated JSON page does not exist — `.../sqlite/json` returns 404;
  the provider index only lists install/engine/limitations:
  [index](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)).
- Size/single-file deploy: `Microsoft.Data.Sqlite` pulls in `SQLitePCLRaw.core` +
  `SQLitePCLRaw.bundle_e_sqlite3` ([NuGet dependencies](https://www.nuget.org/packages/microsoft.data.sqlite)).
  EF Core adds `Microsoft.EntityFrameworkCore(.Relational/.Analyzers)` to the graph.
  In single-file, only managed DLLs go into the bundle; native runtime libraries ship as
  separate files unless `IncludeNativeLibrariesForSelfExtract=true` (extracts to
  disk at startup — `%TEMP%/.net` on Windows)
  ([native libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
  Fewer packages = less trim/AOT surface.
- Future AOT: `Microsoft.Data.Sqlite` is pure ADO.NET — parameterized `SqliteConnection` /
  `SqliteCommand` calls are statically analyzable and do not require runtime code
  generation. EF NativeAOT is **experimental, not recommended for production**,
  requires query precompilation via interceptors + the `Microsoft.EntityFrameworkCore.Tasks` package,
  and **dynamic queries are not supported** (conditional composition of `Where`/`OrderBy`
  — exactly what search with optional filters would do)
  ([NativeAOT + precompiled queries](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries)).
  The port's search (query + exclude flags + type/tag filters) is dynamic by nature.

### Requested confirmations (all OK)

| Item | Verdict | Primary source |
|---|---|---|
| `UNIQUE(type,content)` | Supported: explicit UNIQUE constraint + conflict target in the UPSERT | [UPSERT](https://sqlite.org/lang_upsert.html): "conflict target specifies a uniqueness constraint"; only works for UNIQUE/PK/unique index |
| `BOOLEAN` (0/1) | SQLite has no Boolean storage class; booleans are INTEGER 0/1; `Microsoft.Data.Sqlite` maps `Boolean ↔ INTEGER (0 or 1)` | [Datatypes in SQLite §2.1](https://www.sqlite.org/datatype3.html) + [Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types) |
| `TIMESTAMP` as ISO8601 TEXT | SQLite has no dedicated date type; dates are TEXT/REAL/INTEGER; `Microsoft.Data.Sqlite` maps `DateTime ↔ TEXT yyyy-MM-dd HH:mm:ss.FFFFFFF`; the `date/time/datetime` functions accept/return ISO-8601 | [Datatypes](https://www.sqlite.org/datatype3.html) ("no storage class for dates"), [Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types), [Date functions](https://sqlite.org/lang_datefunc.html) |
| `PRAGMA journal_mode=WAL` | Supported and **persistent** (survives close/reopen); returns `"wal"` on success | [WAL](https://www.sqlite.org/wal.html) + [PRAGMA journal_mode](https://sqlite.org/pragma.html). Activation sample via `Microsoft.Data.Sqlite` in [Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async) |
| Online backup | Supported via `SqliteConnection.BackupDatabase(dest)` (wrapper of the Online Backup API, consistent incremental copy without a long lock) | [API ref BackupDatabase](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase) + [Online Backup API](https://sqlite.org/backup.html) |

Connection/transaction notes relevant to the spec: ADO.NET connection string `Data Source=...;Cache=...;Mode=...;Pooling=...`
([keywords](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings));
**do not combine `Cache=Shared` with WAL** ("mixing shared-cache mode and WAL is discouraged" —
[same page](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings));
transactions are serializable by default, deferred since v5.0, savepoints since v6.0
([Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions));
ADO.NET async methods **run synchronously** (SQLite has no asynchronous I/O) —
do not call them; use WAL for concurrency
([Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)).
`System.Transactions` and `DbDataAdapter` are not implemented
([ADO.NET limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/adonet-limitations)) —
use explicit ADO.NET transactions.

## 2. Ported DDL + SQL for each operation (ready for the spec)

`type` values: `Text/Code/Image/File/Files/Link/Character/Color` (01§1/§3). `pinned` 0/1.
`tag` NULL or one of the 9 values (01§1). `datetime` TEXT ISO-8601 UTC (`...Z`, see §3/note
below). `metadata` TEXT NULL (JSON, §3). `title` TEXT NULL (editable).

```sql
-- Schema v2 (parity with DATABASE_VERSION = 2, 01§3)
CREATE TABLE IF NOT EXISTS clipboard (
  id       INTEGER PRIMARY KEY AUTOINCREMENT,
  type     TEXT    NOT NULL,
  content  TEXT    NOT NULL,
  pinned   INTEGER NOT NULL DEFAULT 0,   -- BOOLEAN as 0/1
  tag      TEXT    NULL,
  datetime TEXT    NOT NULL,              -- ISO-8601 UTC
  metadata TEXT    NULL,                  -- JSON per type (§3)
  title    TEXT    NULL,                  -- added in the v0->v1 migration in the original
  UNIQUE (type, content)
) STRICT;

CREATE TABLE IF NOT EXISTS clipboard_version (
  id      INTEGER PRIMARY KEY CHECK (id = 1),
  version INTEGER NOT NULL
);
INSERT INTO clipboard_version (id, version) VALUES (1, 2)
  ON CONFLICT (id) DO NOTHING;

CREATE INDEX IF NOT EXISTS idx_clipboard_datetime ON clipboard (datetime DESC);
CREATE INDEX IF NOT EXISTS idx_clipboard_protect  ON clipboard (pinned, tag);
```

v0→v1 migration (only if the DB exists without `title`, mirroring the original's
`ALTER TABLE ADD COLUMN title`, 01§3):

```sql
-- If PRAGMA table_info(clipboard) does not contain `title`:
ALTER TABLE clipboard ADD COLUMN title TEXT NULL;
UPDATE clipboard_version SET version = 2 WHERE id = 1;
```

DDL portability notes: `BOOLEAN`/`DATETIME`/`TIMESTAMP` as column names have
NUMERIC affinity, not a native one
([type affinity](https://www.sqlite.org/datatype3.html)) — which is why the DDL above uses
explicit `INTEGER`/`TEXT` (official recommendation: use only the 4 primitive names —
[Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types)).
`STRICT` is optional (rejects values outside the declared type); if the spec wants maximum
fidelity to the original (Gda without enforcement), remove `STRICT`. `AUTOINCREMENT` already implies
`UNIQUE` on `id`; the original's separate `UNIQUE` is redundant. `datetime` format:
the original exporter uses `strftime('%FT%T.000000Z', datetime,'utc')` (01§3) — i.e.
`YYYY-MM-DDTHH:MM:SS.000000Z`; SQLite's date functions accept that format
([time values](https://sqlite.org/lang_datefunc.html)). If the spec prefers the
driver's format (`yyyy-MM-dd HH:mm:ss.FFFFFFF`), the engine accepts it too — **but pick ONE
and document it** (the lexicographic comparison `datetime(...) < ...` and `ORDER BY datetime DESC`
are only correct with a fixed format + UTC).

Activation in `Open()` (once per write connection; WAL is persistent):

```sql
PRAGMA journal_mode = WAL;      -- returns 'wal'; check the returned value
PRAGMA foreign_keys = ON;       -- no FKs in the schema; optional
PRAGMA busy_timeout = 5000;     -- single-writer: serializes contention instead of failing
```

### insert-with-dedup (does the upsert update `datetime`? — YES, with a caveat)

Original semantics (01§3): `selectConflict(type,content)` before inserting; if already
tracked, **it only bumps `datetime` and returns null** (reorders without duplicating); a plain `insert` on
conflict returns null; `track()` merges edit conflicts. In other words: **there are never two
rows with the same `(type,content)`; re-copying = new `datetime`**.

Ported SQL (UPSERT with the conflict target on the UNIQUE constraint — syntax
[documented here](https://sqlite.org/lang_upsert.html), available since 3.24.0/2018):

```sql
-- Insert with dedup: conflict => updates datetime (+ metadata/title of the new content)
INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata, title)
VALUES ($type, $content, $pinned, $tag, $datetime, $metadata, $title)
ON CONFLICT (type, content) DO UPDATE SET
  datetime = excluded.datetime,
  metadata = excluded.metadata,
  title    = excluded.title;
SELECT changes();  -- 1 = inserted, 0 + update rowcount... (see note)
```

Caveat for the spec: with `DO UPDATE`, `changes()` counts the updated row, so
telling "inserted" from "bumped" requires `RETURNING`:

```sql
INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata, title)
VALUES ($type, $content, $pinned, $tag, $datetime, $metadata, $title)
ON CONFLICT (type, content) DO UPDATE SET
  datetime = excluded.datetime,
  metadata = excluded.metadata,
  title    = excluded.title
RETURNING id, (CASE WHEN datetime = $datetime THEN 1 ELSE 1 END);
```

Simpler form, faithful to the original (two statements, same transaction): `SELECT id ...
WHERE type/content` → if it exists, `UPDATE ... SET datetime=$now WHERE id` and return null;
otherwise `INSERT`. Equivalent to the `entryTracker`'s `selectConflict`+bump (01§3), and it avoids
the `RETURNING` ambiguity. **Do NOT use `INSERT OR REPLACE`**: it deletes and reinserts
(new `id`, breaks future FKs and invalidates references) — different semantics from the bump.

### `updateProperty` with conflict detection

Original (01§3): an update to a conflicting `type/content` returns the **id of the conflicting row**
(Gda); the Memory backend only supports updating `content`. Ported (column restricted by an
allowlist in code — `pinned|tag|datetime|metadata|title|type|content`):

```sql
-- Direct attempt; on SQLITE_CONSTRAINT_UNIQUE, resolve:
UPDATE clipboard SET <prop> = $value WHERE id = $id;
-- If it fails with the UNIQUE constraint (only possible for type/content):
SELECT id FROM clipboard WHERE type = $type AND content = $content LIMIT 1;
-- Returns the conflicting id (Gda parity) and does NOT apply the partial edit.
-- For non-unique props (pinned/tag/datetime/metadata/title): update never conflicts.
```

Single-SQL alternative for an edit that merges (mirrors `track()`, which "merges edit
conflicts", 01§3) — the spec chooses between "returns the conflicting id" (Gda) and "merges":

```sql
-- type/content merge: deletes the edited row and bumps the survivor (inside a transaction)
UPDATE clipboard SET type=$t, content=$c, metadata=$m, title=$ti WHERE id=$id;
-- on UNIQUE violation -> DELETE FROM clipboard WHERE id=$id;
--                    -> UPDATE clipboard SET datetime=$now WHERE type=$t AND content=$c;
```

### `clear` (keep pins+tags)

Original (01§3): `KeepAll` → nothing; `KeepPinnedAndTagged` → deletes only
`NOT(pinned OR tag IS NOT NULL)`; `Clear` → everything. Predicate ported 1:1:

```sql
-- KeepPinnedAndTagged (the original's default):
DELETE FROM clipboard WHERE NOT (pinned = 1 OR tag IS NOT NULL);
-- Clear (everything, incl. protected):
DELETE FROM clipboard;
-- KeepAll: no statement.
```

`pinned` is INTEGER 0/1 (§1); a truthy `pinned` alone is enough, but `pinned = 1` is explicit
and immune to garbage. `tag IS NOT NULL` (never `tag <> ''` — absence is NULL in the schema).

### `deleteOldest` (limit N + age M, protecting pins/tags)

Original (01§3): deletes unprotected entries beyond N **UNION** unprotected entries older than M;
`history-length` N (default 50), `history-time` M minutes (default 0 = no limit);
runs on every insert + a 60 s timer when M>0. Ported (one statement, stable ids):

```sql
-- $limit = N (history-length), $cutoff = ISO-8601 UTC of (now - M minutes), $runAge = (M > 0)
DELETE FROM clipboard WHERE id IN (
  SELECT id FROM clipboard
  WHERE NOT (pinned = 1 OR tag IS NOT NULL)
  ORDER BY datetime DESC
  LIMIT -1 OFFSET $limit
)
OR id IN (
  SELECT id FROM clipboard
  WHERE $runAge = 1
    AND NOT (pinned = 1 OR tag IS NOT NULL)
    AND datetime < $cutoff
);
```

Notes: `LIMIT -1 OFFSET N` = "all after the first N" (SQLite accepts `LIMIT -1`);
`ORDER BY datetime DESC` reuses the `idx_clipboard_datetime` index. `$cutoff` computed in
C# (`DateTime.UtcNow.AddMinutes(-M)`, §2 format) — avoids `datetime('now', ...)` with
ambiguous precision/time zone in SQL. When `M = 0`, `$runAge = 0` turns off the second branch
(parity with `history-time = 0 = no limit`, 01§5). The 60 s timer + "defer while the dialog
is open" is UI behavior, not SQL (take it to the extension/timer spec).

### Read / ordering

```sql
SELECT id, type, content, pinned, tag, datetime, metadata, title
FROM clipboard
ORDER BY datetime DESC;
```

Parity: Gda `select_order_by(datetime)` + Memory `sort(b-a)` = most-recent-first
(01§3). Search filters compose an additional `WHERE` (§6). Future pagination: `LIMIT/OFFSET`
over the same ordering.

## 3. `metadata` JSON per type

Content per type (01§3): `Code{language{id,name}|null}`, `File/Files{operation: copy|cut}`,
`Link{title,description,image}`; other types `NULL`. Column `metadata TEXT NULL`;
(de)serialization with `System.Text.Json`:

- Serialize with `JsonSerializer.Serialize` ([how-to](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to));
  deserialize with `JsonSerializer.Deserialize<T>` ([API ref](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize)).
- Use a **reused, static** `JsonSerializerOptions` (construction cost) with
  `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` if the spec wants camel keys
  (`operation`, `language`) identical to the original; minified by default (same as the original).
- For future AOT: prefer `JsonSerializerContext` (source generation) from the start —
  `Deserialize` overloads without a context carry `RequiresDynamicCode`/`RequiresUnreferencedCode`
  ([same ref](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize)).
- C# types: `record CodeMetadata(LanguageRef? Language)`, `record FileMetadata(string Operation)`
  (validate `copy|cut`, default `copy`), `record LinkMetadata(string? Title, string? Description, string? Image)`.
  Deserialize **by the row's `type`**, not by sniffing the JSON.

Validation on read — **corrupt JSON → discard the `metadata` (null), NEVER the entry**:

- `Deserialize` throws `JsonException` ("JSON is invalid / not compatible" —
  [exceptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize));
  catch `JsonException` (+ `NotSupportedException`) per row, log the `id`, carry on with `metadata = null`.
- Rationale: the content (`content`) is the user's data; `metadata` is enrichment
  (detected language, link preview). Losing a preview is tolerable; losing the entry is not.
- Optional cheap validation in SQL: `json_valid(metadata)` (returns 1 if valid RFC-8259 —
  [json_valid](https://www.sqlite.org/json1.html)); `json_valid(x, 6)` also accepts JSON5/JSONB.
  Do not use a CHECK constraint with `json_valid` (it would reject legacy writes; a write failure ≠ a tolerant read).
- `null`/empty/`"null"` → `null` without an exception (handle it before deserializing).

## 4. Files: image bytes and link thumbnails

Ported mapping (original: `getImagesPath` = `images/` child of data; cache via
`getCachePath`; name `<md5>.<ext>` / `MD5(url)` — 01§1/§2):

- Images: `%LocalAppData%\WindowsCM\images\<md5>.<ext>` (ext from the mimetype, e.g. `.png`;
  only written if it does not exist — 01§2). The entry's `content` = `file://...` URI (parity).
- Link thumbnails: `%LocalAppData%\WindowsCM\cache\<md5>` (md5 of the URL; a hit skips the download — 01§8).
- Base via `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`
  ([enum](https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder):
  `LocalApplicationData` = non-roaming user data = `%LOCALAPPDATA%`).

Transactional cleanup (decision for the spec):

1. **Delete the file ONLY after the DB transaction commits** (order: `BEGIN` → `DELETE`
   row → `COMMIT` → `File.Delete`). Reason: files do not take part in SQLite transactions;
   deleting first and then failing the commit = an entry without a file (worse than an orphan). Explicit
   ADO.NET transactions: [Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)
   (no `System.Transactions` — [limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/adonet-limitations)).
2. Bulk `clear`/`deleteOldest`: collect the `content` of the affected entries **first**
   (`SELECT ... RETURNING content` or SELECT-then-DELETE in the same transaction), commit,
   then delete the files in a batch, best-effort (an IO failure does not undo the commit).
3. **Orphan GC at startup**: sweep `images/` and `cache/`, delete files with no referencing
   entry (`SELECT content` → set of names; the rest are orphans of a crash between commit
   and `File.Delete`). Does it also cover the reverse case? No — an entry without a file is shown with
   a fallback (placeholder), never deleted by the GC.
4. Images shared through dedup: since `(type,content)` is UNIQUE, the same file
   never has 2 owners — simple deletion, no refcount. (If a future spec allows
   duplicates, revisit with counting.)

## 5. Backends: SQLite only (+ Memory for tests)

**Recommendation: SQLite in production + Memory for tests; JSON out.**

- The original had the order Default → `.db` exists → `.json` exists → try SQLite → try
  JSON → memory, with the `DEBUG_COPYOUS_DBPATH` override (01§3). For WindowsCM: **no
  silent fallback** — a fallback masks corruption/loss (the user thinks they lost the
  history when it actually fell back to memory). If SQLite fails at startup: a visible
  error + an option to recreate; never silent memory.
- JSON backend out: same in-memory semantics + debounced 1000 ms persistence (01§3)
  = a loss window on every crash + the cost of a full rewrite + manual ISO-8601.
  Keeping a JSON backend doubles the test matrix (dedup/upsert/clear/deleteOldest × 2)
  with no benefit — single-file SQLite is already "just one file".
- Memory kept **only** as `Data Source=:memory:` via `Microsoft.Data.Sqlite`
  (same implementation, [connection strings](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings))
  for the repository's unit tests (no disk, no SQL mock).
- Default path: `%LocalAppData%\WindowsCM\clipboard.db` (+ adjacent `-wal`/`-shm` in WAL —
  [WAL file](https://www.sqlite.org/wal.html): never copy/move the `.db` without them while
  connections are open; backup via `BackupDatabase`, §1).
- Override (precedence): `WINDOWSCM_DBPATH` (env, debug/automation — mirrors
  `DEBUG_COPYOUS_DBPATH`, 01§3) > `database-location` (settings) > default. Validate:
  directory exists/can be created, `.db` extension; an empty env var = ignored.

## 6. Search / behavior (pref → implementation mapping)

Base: `Behavior` (`remember-search` F, `exclude-pinned` F, `exclude-tagged` F,
`protect-pinned` T, `protect-tagged` T, `sync-primary` F, `update-date-on-copy` T — 01§5)
+ `SearchQuery{query,pinned,tag,type}` with a locale-insensitive substring (`Intl.Collator`
sensitivity base) and fallback to `entry.title` (01§7).

| Pref | Implementation in the port |
|---|---|
| `Remember Search` (F) | **Where to persist: session settings (not in the DB).** `false` = clears the query when the dialog is closed/reopened (parity: `remember-search=F` clears on unmap — 01§7). `true` = stores the last `{query,pinned,tag,type}` in settings (registry/`appsettings`/the prefs spec's store) and restores it on open. Never in the `clipboard` table. |
| `Exclude Pinned` / `Exclude Tagged` (F) | Default filters of the read query: if `exclude-pinned`, append `AND pinned = 0`; if `exclude-tagged`, `AND tag IS NULL`. Equivalent to the `SearchQuery` exclude flags that "re-trigger" the search (01§7). Defaults F = no clause. |
| `Protect Pinned` / `Protect Tagged` (T) | `WHERE` in the deletions, **not** a separate lock: `deleteOldest` and `clear(KeepPinnedAndTagged)` already carry `NOT (pinned = 1 OR tag IS NOT NULL)` (§2). `protect-*=F` removes the corresponding branch of the predicate (e.g. only `NOT pinned` if `protect-tagged=F`). Manual delete of a protected item: blocked unless Shift=force (UI parity — 01§7; take it to the UI spec, not SQL). |
| `Sync Primary` (F) | **N/A on Windows — lock it as out.** Win32 exposes ONE clipboard per window station (sequence via `GetClipboardSequenceNumber`, viewers/listeners — [About](https://learn.microsoft.com/en-us/windows/win32/dataxchg/about-the-clipboard), [Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations), [Using](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard)); the X11 CLIPBOARD vs PRIMARY dichotomy does not exist. No pref, no code, no telemetry. Document in the spec as "ported as a permanent no-op". |
| `Update Date on Copy` (T) | `UPDATE clipboard SET datetime = $now WHERE id = $id` when copying from history (parity: `update-date-on-copy` refreshes `datetime` — 01§2). Effect: the just-copied item rises to the top (`ORDER BY datetime DESC`). Implement in the copy-from-history handler, not in the insert path (which already stamps `$now`). |

Text search (`Intl.Collator` base parity): in SQLite, `LIKE` is case-insensitive only
for ASCII by default; port as `WHERE content LIKE '%q%' ESCAPE '\' COLLATE NOCASE`
for the simple case + an `OR title LIKE ...` fallback (fallback-to-title parity, 01§7),
and do the locale-insensitive folding (accents) in C# over the result set if the spec requires
full parity — do **NOT** register a custom `COLLATE`/`LIKE` via `CreateFunction` in v1
(AOT/native surface to validate later). Escape `%_[\` in the user's query; an empty query
= no text predicate (only pinned/tag/type filters).
