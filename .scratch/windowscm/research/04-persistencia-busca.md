# 04 — Persistência + busca + histórico (fontes primárias)

Base portada de `01-parity-inventory.md` §§3 (DB) e 5 (prefs Behavior) — sem re-auditoria
do Copyous. Todas as claims de API abaixo citam fonte primária (Microsoft Learn .NET /
SQLite.org / Win32 docs). DDL + SQL prontos para a spec.

## 1. `Microsoft.Data.Sqlite` vs `EF Core Sqlite` — recomendação: `Microsoft.Data.Sqlite`

**Recomendação: `Microsoft.Data.Sqlite` direto (ADO.NET), sem EF Core.** Justificativa:

- O provider EF Core para SQLite é construído em cima de `Microsoft.Data.Sqlite`
  ([overview](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)), logo
  usar a camada base elimina o change tracker, o pipeline LINQ e as migrações sem
  perder nada que o porte precise (o porte é SQL fixo + DDL versionada à mão).
- Controle SQL direto exigido pela paridade: `UNIQUE(type,content)` + upsert com
  conflito-alvo, `DELETE ... WHERE NOT(pinned OR tag IS NOT NULL)`, `deleteOldest`
  com UNION/LIMIT e `ORDER BY datetime DESC` precisam de SQL literal parametrizado;
  no EF isso viraria LINQ dinâmico + `Sql()` de escape — sem ganho.
- Migrations: o engine SQLite não suporta várias operações de schema; o provider EF
  faz rebuild (cria nova tabela, copia, drop, rename) e joga `NotSupportedException`
  para artefatos fora do modelo
  ([tabela de operações + workaround](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)).
  O Copyous versiona o schema à mão (`DATABASE_VERSION=2`, `ALTER TABLE ADD COLUMN title`
  na migração v0→v1 — 01§3); portar isso são ~15 linhas de `CREATE TABLE IF NOT EXISTS` /
  `ALTER TABLE` + `clipboard_version`, contra trazer o pacote de migrations inteiro
  (que ainda cria a tabela `__EFMigrationsLock` e exige limpeza manual em kill
  durante migração — [mesma página](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)).
- JSON: `metadata` é coluna TEXT opaca com (de)serialização em `System.Text.Json`
  (§3). SQLite ≥3.38 já traz as funções JSON embutidas por padrão
  ([compiling in JSON support](https://www.sqlite.org/json1.html)), então validação
  (`json_valid`) existe no engine sem EF. Não há necessidade de mapeamento JSON do EF
  (a página dedicada de JSON do provider não existe — `.../sqlite/json` retorna 404;
  o índice do provider só lista install/engine/limitações:
  [index](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)).
- Tamanho/deploy single-file: `Microsoft.Data.Sqlite` puxa `SQLitePCLRaw.core` +
  `SQLitePCLRaw.bundle_e_sqlite3` ([dependências NuGet](https://www.nuget.org/packages/microsoft.data.sqlite)).
  EF Core adiciona `Microsoft.EntityFrameworkCore(.Relational/.Analyzers)` ao grafo.
  Em single-file, só DLLs gerenciadas vão para o bundle; nativas do runtime saem em
  arquivos separados salvo `IncludeNativeLibrariesForSelfExtract=true` (extrai para
  disco no startup — `%TEMP%/.net` no Windows)
  ([native libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
  Menos pacotes = menos superfície de trim/AOT.
- AOT-futuro: `Microsoft.Data.Sqlite` é ADO.NET puro — chamadas `SqliteConnection` /
  `SqliteCommand` parametrizadas são estaticamente analisáveis e não exigem geração
  de código em runtime. EF NativeAOT é **experimental, não recomendado para produção**,
  exige pré-compilação de queries via interceptors + pacote `Microsoft.EntityFrameworkCore.Tasks`,
  e **queries dinâmicas não são suportadas** (composição condicional de `Where`/`OrderBy`
  — exatamente o que a busca com filtros opcionais faria)
  ([NativeAOT + precompiled queries](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries)).
  A busca do porte (query + flags exclude + filtros de tipo/tag) é dinâmica por natureza.

### Confirmações pedidas (todas OK)

| Item | Veredito | Fonte primária |
|---|---|---|
| `UNIQUE(type,content)` | Suportado: constraint UNIQUE explícita + alvo de conflito no UPSERT | [UPSERT](https://sqlite.org/lang_upsert.html): "conflict target specifies a uniqueness constraint"; só funciona para UNIQUE/PK/índice único |
| `BOOLEAN` (0/1) | SQLite não tem storage class Boolean; booleans são INTEGER 0/1; `Microsoft.Data.Sqlite` mapeia `Boolean ↔ INTEGER (0 ou 1)` | [Datatypes in SQLite §2.1](https://www.sqlite.org/datatype3.html) + [Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types) |
| `TIMESTAMP` como TEXT ISO8601 | SQLite não tem tipo data dedicado; datas são TEXT/REAL/INTEGER; `Microsoft.Data.Sqlite` mapeia `DateTime ↔ TEXT yyyy-MM-dd HH:mm:ss.FFFFFFF`; funções `date/time/datetime` aceitam/retornam ISO-8601 | [Datatypes](https://www.sqlite.org/datatype3.html) ("no storage class for dates"), [Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types), [Date functions](https://sqlite.org/lang_datefunc.html) |
| `PRAGMA journal_mode=WAL` | Suportado e **persistente** (sobrevive a close/reopen); retorna `"wal"` em caso de sucesso | [WAL](https://www.sqlite.org/wal.html) + [PRAGMA journal_mode](https://sqlite.org/pragma.html). Amostra de ativação via `Microsoft.Data.Sqlite` em [Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async) |
| Backup online | Suportado via `SqliteConnection.BackupDatabase(dest)` (wrapper da Online Backup API, cópia incremental consistente sem lock longo) | [API ref BackupDatabase](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase) + [Online Backup API](https://sqlite.org/backup.html) |

Notas de conexão/transação relevantes à spec: connection string ADO.NET `Data Source=...;Cache=...;Mode=...;Pooling=...`
([keywords](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings));
**não combinar `Cache=Shared` com WAL** ("mixing shared-cache mode and WAL is discouraged" —
[mesma página](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings));
transações serializáveis por padrão, deferred desde v5.0, savepoints desde v6.0
([Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions));
métodos async do ADO.NET **executam de forma síncrona** (SQLite sem I/O assíncrono) —
não chamar; usar WAL para concorrência
([Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)).
`System.Transactions` e `DbDataAdapter` não implementados
([ADO.NET limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/adonet-limitations)) —
usar transações ADO.NET explícitas.

## 2. DDL portado + SQL de cada operação (prontos para a spec)

Tipos `type`: `Text/Code/Image/File/Files/Link/Character/Color` (01§1/§3). `pinned` 0/1.
`tag` NULL ou um dos 9 valores (01§1). `datetime` TEXT ISO-8601 UTC (`...Z`, ver §3/nota
abaixo). `metadata` TEXT NULL (JSON, §3). `title` TEXT NULL (editável).

```sql
-- Schema v2 (paridade com DATABASE_VERSION = 2, 01§3)
CREATE TABLE IF NOT EXISTS clipboard (
  id       INTEGER PRIMARY KEY AUTOINCREMENT,
  type     TEXT    NOT NULL,
  content  TEXT    NOT NULL,
  pinned   INTEGER NOT NULL DEFAULT 0,   -- BOOLEAN como 0/1
  tag      TEXT    NULL,
  datetime TEXT    NOT NULL,              -- ISO-8601 UTC
  metadata TEXT    NULL,                  -- JSON por tipo (§3)
  title    TEXT    NULL,                  -- adicionado na migração v0->v1 no original
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

Migração v0→v1 (só se o DB existir sem `title`, espelhando `ALTER TABLE ADD COLUMN title`
do original, 01§3):

```sql
-- Se PRAGMA table_info(clipboard) não contém `title`:
ALTER TABLE clipboard ADD COLUMN title TEXT NULL;
UPDATE clipboard_version SET version = 2 WHERE id = 1;
```

Notas de portabilidade do DDL: `BOOLEAN`/`DATETIME`/`TIMESTAMP` como nomes de coluna têm
afinidade NUMERIC, não nativa
([type affinity](https://www.sqlite.org/datatype3.html)) — por isso a DDL acima usa
`INTEGER`/`TEXT` explícitos (recomendação oficial: usar só os 4 nomes primitivos —
[Data types](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types)).
`STRICT` é opcional (rejeita valores fora do tipo declarado); se a spec quiser máxima
fidelidade ao original (Gda sem enforcement), remover `STRICT`. `AUTOINCREMENT` já implica
`UNIQUE` no `id`; o `UNIQUE` separado do original é redundante. Formato de `datetime`:
o exportador original usa `strftime('%FT%T.000000Z', datetime,'utc')` (01§3) — i.e.
`YYYY-MM-DDTHH:MM:SS.000000Z`; funções de data do SQLite aceitam esse formato
([time values](https://sqlite.org/lang_datefunc.html)). Se a spec preferir o formato do
driver (`yyyy-MM-dd HH:mm:ss.FFFFFFF`), também é aceito pelo engine — **mas escolher UM
e documentar** (comparação lexicográfica `datetime(...) < ...` e `ORDER BY datetime DESC`
só são corretas com formato fixo + UTC).

Ativação no `Open()` (uma vez por conexão de escrita; WAL é persistente):

```sql
PRAGMA journal_mode = WAL;      -- retorna 'wal'; checar o valor retornado
PRAGMA foreign_keys = ON;       -- sem FKs no schema; opcional
PRAGMA busy_timeout = 5000;     -- single-writer: serializa contenção em vez de falhar
```

### insert-com-dedup (upsert atualiza `datetime`? — SIM, com ressalva)

Semântica original (01§3): `selectConflict(type,content)` antes de inserir; se já
tracked, **só bumpa `datetime` e retorna null** (reordena sem duplicar); `insert` puro em
conflito retorna null; `track()` funde conflito de edição. Ou seja: **nunca há duas
linhas com o mesmo `(type,content)`; re-copiar = `datetime` novo**.

SQL portado (UPSERT com alvo de conflito na constraint UNIQUE — sintaxe
[documentada aqui](https://sqlite.org/lang_upsert.html), disponível desde 3.24.0/2018):

```sql
-- Inserção com dedup: conflito => atualiza datetime (+ metadados/título do novo conteúdo)
INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata, title)
VALUES ($type, $content, $pinned, $tag, $datetime, $metadata, $title)
ON CONFLICT (type, content) DO UPDATE SET
  datetime = excluded.datetime,
  metadata = excluded.metadata,
  title    = excluded.title;
SELECT changes();  -- 1 = inseriu, 0 + rowcount de update... (ver nota)
```

Ressalva para a spec: no `DO UPDATE`, `changes()` conta a linha atualizada, então
distinguir "inseriu" vs "bumpou" exige `RETURNING`:

```sql
INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata, title)
VALUES ($type, $content, $pinned, $tag, $datetime, $metadata, $title)
ON CONFLICT (type, content) DO UPDATE SET
  datetime = excluded.datetime,
  metadata = excluded.metadata,
  title    = excluded.title
RETURNING id, (CASE WHEN datetime = $datetime THEN 1 ELSE 1 END);
```

Forma mais simples e fiel ao original (duas statements, mesma transação): `SELECT id ...
WHERE type/content` → se existe, `UPDATE ... SET datetime=$now WHERE id` e retorna null;
senão `INSERT`. Equivale ao `selectConflict`+bump do `entryTracker` (01§3) e evita
ambiguidade do `RETURNING`. **NÃO usar `INSERT OR REPLACE`**: ele deleta e reinsere
(novo `id`, quebra FKs futuras e invalida referências) — semântica diferente do bump.

### `updateProperty` com detecção de conflito

Original (01§3): update em `type/content` conflitante retorna o **id do conflitante**
(Gda); backend Memory só suporta update de `content`. Portado (coluna restrita por
allowlist no código — `pinned|tag|datetime|metadata|title|type|content`):

```sql
-- Tentativa direta; em SQLITE_CONSTRAINT_UNIQUE, resolver:
UPDATE clipboard SET <prop> = $value WHERE id = $id;
-- Se falhar com constraint UNIQUE (só possível para type/content):
SELECT id FROM clipboard WHERE type = $type AND content = $content LIMIT 1;
-- Retorna o id conflitante (paridade Gda) e NÃO aplica a edição parcial.
-- Para props não-únicas (pinned/tag/datetime/metadata/title): update nunca conflita.
```

Alternativa em SQL único para edição que funde (espelha `track()` que "funde conflito
de edição", 01§3) — a spec escolhe entre "retorna id conflitante" (Gda) e "funde":

```sql
-- Fusão type/content: apaga o editado e bumpa o sobrevivente (dentro de transação)
UPDATE clipboard SET type=$t, content=$c, metadata=$m, title=$ti WHERE id=$id;
-- on UNIQUE violation -> DELETE FROM clipboard WHERE id=$id;
--                    -> UPDATE clipboard SET datetime=$now WHERE type=$t AND content=$c;
```

### `clear` (manter pins+tags)

Original (01§3): `KeepAll` → nada; `KeepPinnedAndTagged` → apaga só
`NOT(pinned OR tag IS NOT NULL)`; `Clear` → tudo. Predicado portado 1:1:

```sql
-- KeepPinnedAndTagged (default do original):
DELETE FROM clipboard WHERE NOT (pinned = 1 OR tag IS NOT NULL);
-- Clear (tudo, incl. protegidos):
DELETE FROM clipboard;
-- KeepAll: nenhuma statement.
```

`pinned` é INTEGER 0/1 (§1); `pinned` truthy sozinho basta, mas `pinned = 1` é explícito
e imune a lixo. `tag IS NOT NULL` (nunca `tag <> ''` — ausência é NULL no schema).

### `deleteOldest` (limite N + idade M, protegendo pins/tags)

Original (01§3): apaga não-protegidos além de N **UNION** não-protegidos mais velhos que M;
`history-length` N (default 50), `history-time` M minutos (default 0 = sem limite);
roda a cada insert + timer de 60 s quando M>0. Portado (uma statement, ids estáveis):

```sql
-- $limit = N (history-length), $cutoff = ISO-8601 UTC de (now - M minutos), $runAge = (M > 0)
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

Notas: `LIMIT -1 OFFSET N` = "todos após os N primeiros" (SQLite aceita `LIMIT -1`);
`ORDER BY datetime DESC` reuse o índice `idx_clipboard_datetime`. `$cutoff` calculado no
C# (`DateTime.UtcNow.AddMinutes(-M)`, formato §2) — evita `datetime('now', ...)` com
precisão/fuso ambíguos no SQL. Quando `M = 0`, `$runAge = 0` desliga o segundo ramo
(paridade com `history-time = 0 = sem limite`, 01§5). Timer de 60 s + "adiar com diálogo
aberto" é comportamento de UI, não do SQL (levar para a spec de extensão/timer).

### Leitura / ordenação

```sql
SELECT id, type, content, pinned, tag, datetime, metadata, title
FROM clipboard
ORDER BY datetime DESC;
```

Paridade: Gda `select_order_by(datetime)` + Memory `sort(b-a)` = mais-recente-primeiro
(01§3). Filtros de busca compõem `WHERE` adicional (§6). Paginação futura: `LIMIT/OFFSET`
sobre a mesma ordenação.

## 3. `metadata` JSON por tipo

Conteúdo por tipo (01§3): `Code{language{id,name}|null}`, `File/Files{operation: copy|cut}`,
`Link{title,description,image}`; demais tipos `NULL`. Coluna `metadata TEXT NULL`;
(de)serialização com `System.Text.Json`:

- Serializar com `JsonSerializer.Serialize` ([how-to](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to));
  desserializar com `JsonSerializer.Deserialize<T>` ([API ref](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize)).
- Usar `JsonSerializerOptions` **reutilizada e estática** (custo de construção) com
  `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` se a spec quiser chaves camel
  (`operation`, `language`) iguais ao original; minificado por padrão (igual ao original).
- Para AOT-futuro: preferir `JsonSerializerContext` (source generation) desde o início —
  overloads `Deserialize` sem contexto carregam `RequiresDynamicCode`/`RequiresUnreferencedCode`
  ([mesma ref](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize)).
- Tipos C#: `record CodeMetadata(LanguageRef? Language)`, `record FileMetadata(string Operation)`
  (validar `copy|cut`, default `copy`), `record LinkMetadata(string? Title, string? Description, string? Image)`.
  Desserializar **pelo `type` da linha**, não por sniffing do JSON.

Validação na leitura — **JSON corrompido → descarta o `metadata` (null), NUNCA a entry**:

- `Deserialize` lança `JsonException` ("JSON is invalid / not compatible" —
  [exceptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize));
  capturar `JsonException` (+ `NotSupportedException`) por linha, logar `id`, seguir com `metadata = null`.
- Justificativa: o conteúdo (`content`) é o dado do usuário; `metadata` é enriquecimento
  (idioma detectado, preview de link). Perder preview é tolerável; perder a entry, não.
- Validação barata opcional no SQL: `json_valid(metadata)` (retorna 1 se RFC-8259 válido —
  [json_valid](https://www.sqlite.org/json1.html)); `json_valid(x, 6)` aceita também JSON5/JSONB.
  Não usar CHECK constraint com `json_valid` (rejeitaria writes legados; falha de escrita ≠ leitura tolerante).
- `null`/vazio/`"null"` → `null` sem exceção (tratar antes de desserializar).

## 4. Arquivos: bytes de imagem e thumbnails de link

Mapeamento portado (original: `getImagesPath` = `images/` filho de data; cache via
`getCachePath`; nome `<md5>.<ext>` / `MD5(url)` — 01§1/§2):

- Imagens: `%LocalAppData%\WindowsCM\images\<md5>.<ext>` (ext do mimetype, ex. `.png`;
  só grava se não existir — 01§2). `content` da entry = URI `file://...` (paridade).
- Thumbnails de link: `%LocalAppData%\WindowsCM\cache\<md5>` (md5 da URL; hit pula download — 01§8).
- Base via `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`
  ([enum](https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder):
  `LocalApplicationData` = dados de usuário não-roaming = `%LOCALAPPDATA%`).

Limpeza transacional (decisão para a spec):

1. **Deletar o arquivo SÓ após o commit da transação do DB** (ordem: `BEGIN` → `DELETE`
   row → `COMMIT` → `File.Delete`). Motivo: arquivos não participam de transação SQLite;
   deletar antes e falhar o commit = entry sem arquivo (pior que órfão). Transações
   ADO.NET explícitas: [Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)
   (sem `System.Transactions` — [limitações](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/adonet-limitations)).
2. `clear`/`deleteOldest` em massa: coletar `content` das entries afetadas **antes**
   (`SELECT ... RETURNING content` ou SELECT-then-DELETE na mesma transação), commitar,
   depois deletar arquivos em lote com best-effort (falha de IO não desfaz o commit).
3. **GC de órfãos no startup**: varrer `images/` e `cache/`, deletar arquivos sem entry
   referenciadora (`SELECT content` → set de nomes; o resto é órfão de crash entre commit
   e `File.Delete`). Também cobre o caso inverso? Não — entry sem arquivo é exibida com
   fallback (placeholder), nunca deletada pelo GC.
4. Imagens compartilhadas por dedup: como `(type,content)` é UNIQUE, o mesmo arquivo
   nunca tem 2 owners — deleção simples, sem refcount. (Se a spec futura permitir
   duplicatas, revisitar com contagem.)

## 5. Backends: só SQLite (+ Memory para testes)

**Recomendação: SQLite em produção + Memory para testes; JSON fora.**

- Original tinha ordem Default → `.db` existe → `.json` existe → tenta SQLite → tenta
  JSON → memory, com override `DEBUG_COPYOUS_DBPATH` (01§3). Para WindowsCM: **sem
  fallback silencioso** — fallback mascara corrupção/perda (usuário acha que perdeu o
  histórico quando na verdade caiu no memory). Se o SQLite falhar no startup: erro
  visível + opção de recriar; nunca memory silencioso.
- JSON backend fora: mesma semântica em memória + persistência debounced 1000 ms (01§3)
  = janela de perda a cada crash + custo de reescrita integral + ISO-8601 manual.
  Manter um backend JSON duplica a matriz de teste (dedup/upsert/clear/deleteOldest × 2)
  sem benefício — SQLite single-file já é "só um arquivo".
- Memory mantido **apenas** como `Data Source=:memory:` via `Microsoft.Data.Sqlite`
  (mesma implementação, [connection strings](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings))
  para testes unitários do repositório (sem disco, sem mock de SQL).
- Path default: `%LocalAppData%\WindowsCM\clipboard.db` (+ `-wal`/`-shm` adjacentes em WAL —
  [WAL file](https://www.sqlite.org/wal.html): nunca copiar/mover o `.db` sem eles com
  conexões abertas; backup via `BackupDatabase`, §1).
- Override (precedência): `WINDOWSCM_DBPATH` (env, debug/automação — espelha
  `DEBUG_COPYOUS_DBPATH`, 01§3) > `database-location` (settings) > default. Validar:
  diretório existe/criável, extensão `.db`; env var vazia = ignorada.

## 6. Busca / comportamento (mapeamento pref → implementação)

Base: `Behavior` (`remember-search` F, `exclude-pinned` F, `exclude-tagged` F,
`protect-pinned` T, `protect-tagged` T, `sync-primary` F, `update-date-on-copy` T — 01§5)
+ `SearchQuery{query,pinned,tag,type}` com substring locale-insensível (`Intl.Collator`
sensitivity base) e fallback para `entry.title` (01§7).

| Pref | Implementação no porte |
|---|---|
| `Remember Search` (F) | **Onde persistir: settings da sessão (não no DB).** `false` = limpa a query ao fechar/reabrir o diálogo (paridade `remember-search=F` limpa ao desmapear — 01§7). `true` = guarda última `{query,pinned,tag,type}` em settings (registry/`appsettings`/store da spec de prefs) e restaura ao abrir. Nunca na tabela `clipboard`. |
| `Exclude Pinned` / `Exclude Tagged` (F) | Filtros default da query de leitura: se `exclude-pinned`, acrescentar `AND pinned = 0`; se `exclude-tagged`, `AND tag IS NULL`. Equivalem aos flags exclude do `SearchQuery` que "re-disparam" a busca (01§7). Defaults F = sem cláusula. |
| `Protect Pinned` / `Protect Tagged` (T) | `WHERE` nas deleções, **não** bloqueio separado: `deleteOldest` e `clear(KeepPinnedAndTagged)` já carregam `NOT (pinned = 1 OR tag IS NOT NULL)` (§2). `protect-*=F` remove o respectivo ramo do predicado (ex.: só `NOT pinned` se `protect-tagged=F`). Delete manual de item protegido: bloqueado salvo Shift=force (paridade UI — 01§7; levar para spec de UI, não SQL). |
| `Sync Primary` (F) | **N/A Windows — trancar como fora.** O Win32 expõe UM clipboard por window station (sequência via `GetClipboardSequenceNumber`, viewers/listeners — [About](https://learn.microsoft.com/en-us/windows/win32/dataxchg/about-the-clipboard), [Operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations), [Using](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard)); não existe a dicotomia X11 CLIPBOARD vs PRIMARY. Sem pref, sem código, sem telemetria. Documentar na spec como "portado como no-op permanente". |
| `Update Date on Copy` (T) | `UPDATE clipboard SET datetime = $now WHERE id = $id` ao copiar do histórico (paridade `update-date-on-copy` refresca `datetime` — 01§2). Efeito: item recém-copiado sobe ao topo (`ORDER BY datetime DESC`). Implementar no handler copy-from-history, não no insert path (que já carimba `$now`). |

Busca textual (paridade `Intl.Collator` base): no SQLite, `LIKE` é case-insensitive só
para ASCII por padrão; portar como `WHERE content LIKE '%q%' ESCAPE '\' COLLATE NOCASE`
para o caso simples + fallback `OR title LIKE ...` (paridade fallback-para-title, 01§7),
e fazer o folding locale-insensível (acentos) em C# sobre o result set se a spec exigir
paridade total — **NÃO** registrar `COLLATE`/`LIKE` custom via `CreateFunction` na v1
(superfície AOT/native a validar depois). Escapar `%_[\` na query do usuário; query vazia
= sem predicado textual (só filtros pinned/tag/type).
