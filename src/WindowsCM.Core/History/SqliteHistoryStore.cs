// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.Data.Sqlite;

namespace WindowsCM.Core.History;

// SQLite history store (schema v2, Copyous parity). Dates are TEXT
// "yyyy-MM-dd HH:mm:ss" UTC: fixed width, lexicographically sortable.
// The connection stays open for the store lifetime (required for :memory:).
public sealed class SqliteHistoryStore : IHistoryStore
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    // SQLITE_MAX_LIKE_PATTERN_LENGTH is 50,000 bytes; stay clear of it.
    private const int MaxLikePatternBytes = 40_000;

    // Corrupt metadata reads as NULL. SQLite validates it in place: parsing
    // every row's JSON in .NET on every read cost ~80 ms per popup open with
    // 30 items copied from a browser or VS Code (their CF_HTML is stored in
    // the metadata, hundreds of KB each).
    private const string MetadataColumn = "CASE WHEN json_valid(metadata) THEN metadata END";

    public SqliteHistoryStore(string connectionString)
    {
        EnsureParentDirectory(connectionString);
        _connection = new SqliteConnection(connectionString);
        try
        {
            _connection.Open();
            EnsureSchema();
        }
        catch
        {
            // A damaged file fails here; the handle must not outlive the
            // throw, or the file stays locked and cannot be moved aside.
            _connection.Dispose();
            throw;
        }
    }

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        var existing = FindId(item.Kind, item.Content);
        if (existing is long id)
        {
            var metadata = Previews.ItemMetadataJson.OnRecopy(ReadMetadata(id), item.MetadataJson);
            using var bump = _connection.CreateCommand();
            // A re-copy carries no title: keep the item's own (set by the
            // user or by a link preview) instead of wiping it, and merge the
            // metadata (OnRecopy).
            bump.CommandText = """
                UPDATE clipboard SET datetime = $datetime, metadata = $metadata, title = COALESCE($title, title)
                WHERE id = $id
                """;
            bump.Parameters.AddWithValue("$datetime", Stamp(item.CapturedAt));
            bump.Parameters.AddWithValue("$metadata", (object?)metadata ?? DBNull.Value);
            bump.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
            bump.Parameters.AddWithValue("$id", id);
            bump.ExecuteNonQuery();
            return ReadById(id)!;
        }

        using var insert = _connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata, title)
            VALUES ($type, $content, $pinned, $tag, $datetime, $metadata, $title);
            SELECT last_insert_rowid();
            """;
        Bind(insert, item);
        var newId = (long)insert.ExecuteScalar()!;
        return item with { Id = newId };
    }

    public IReadOnlyList<ClipboardItem> List()
    {
        using var query = _connection.CreateCommand();
        query.CommandText = $"""
            SELECT id, type, content, pinned, tag, datetime, {MetadataColumn}, title
            FROM clipboard
            ORDER BY datetime DESC
            """;
        using var reader = query.ExecuteReader();
        return ReadAll(reader);
    }

    public ClipboardItem? GetById(long id) => ReadById(id);

    public IReadOnlyList<string> ImageContents()
    {
        using var query = _connection.CreateCommand();
        query.CommandText = "SELECT content FROM clipboard WHERE type = $type";
        query.Parameters.AddWithValue("$type", nameof(ItemKind.Image));
        using var reader = query.ExecuteReader();
        var contents = new List<string>();
        while (reader.Read())
        {
            contents.Add(reader.GetString(0));
        }
        return contents;
    }

    // Streams the datetime index and stops at the first readable row, so it
    // skips unknown future types exactly like List() does.
    public ClipboardItem? GetLatest()
    {
        using var query = _connection.CreateCommand();
        query.CommandText = $"""
            SELECT id, type, content, pinned, tag, datetime, {MetadataColumn}, title
            FROM clipboard
            ORDER BY datetime DESC
            """;
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            if (ReadItem(reader) is { } item)
            {
                return item;
            }
        }
        return null;
    }

    public long TryUpdateContent(long id, ItemKind kind, string content)
    {
        using var update = _connection.CreateCommand();
        update.CommandText = "UPDATE clipboard SET type = $type, content = $content WHERE id = $id";
        update.Parameters.AddWithValue("$type", kind.ToString());
        update.Parameters.AddWithValue("$content", content);
        update.Parameters.AddWithValue("$id", id);
        try
        {
            update.ExecuteNonQuery();
            return -1;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return FindId(kind, content) ?? -1;
        }
    }

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true)
    {
        var guard = keepProtected ? UnprotectedWhere(protectPinned, protectTagged) : "";
        using var clear = _connection.CreateCommand();
        clear.CommandText = string.IsNullOrEmpty(guard)
            ? "DELETE FROM clipboard"
            : $"DELETE FROM clipboard WHERE {guard}";
        return clear.ExecuteNonQuery();
    }

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true)
    {
        var guard = UnprotectedWhere(protectPinned, protectTagged);
        var countBranch = string.IsNullOrEmpty(guard)
            ? "SELECT id FROM clipboard ORDER BY datetime DESC LIMIT -1 OFFSET $limit"
            : $"SELECT id FROM clipboard WHERE {guard} ORDER BY datetime DESC LIMIT -1 OFFSET $limit";
        var ageBranch = string.IsNullOrEmpty(guard)
            ? "SELECT id FROM clipboard WHERE $runAge = 1 AND datetime < $cutoff"
            : $"SELECT id FROM clipboard WHERE $runAge = 1 AND {guard} AND datetime < $cutoff";
        using var evict = _connection.CreateCommand();
        evict.CommandText = $"DELETE FROM clipboard WHERE id IN ({countBranch}) OR id IN ({ageBranch})";
        evict.Parameters.AddWithValue("$limit", maxCount);
        evict.Parameters.AddWithValue("$runAge", maxAgeMinutes > 0 ? 1 : 0);
        evict.Parameters.AddWithValue("$cutoff", Stamp(utcNow.AddMinutes(-maxAgeMinutes)));
        return evict.ExecuteNonQuery();
    }

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false)
    {
        using var search = _connection.CreateCommand();
        var sql = new System.Text.StringBuilder($"""
            SELECT id, type, content, pinned, tag, datetime, {MetadataColumn}, title
            FROM clipboard WHERE 1 = 1
            """);
        if (!string.IsNullOrEmpty(query))
        {
            var like = $"%{EscapeLike(query)}%";
            if (System.Text.Encoding.UTF8.GetByteCount(like) <= MaxLikePatternBytes)
            {
                sql.Append(" AND (content LIKE $like ESCAPE '\\' COLLATE NOCASE");
                sql.Append(" OR title LIKE $like ESCAPE '\\' COLLATE NOCASE)");
                search.Parameters.AddWithValue("$like", like);
            }
            else
            {
                // SQLite refuses LIKE patterns over 50,000 bytes ("pattern
                // too complex"), so a long pasted line threw on every
                // refresh. Such a query is matched literally instead, with
                // the same ASCII-only case folding as LIKE.
                sql.Append(" AND (instr(lower(content), lower($needle)) > 0 OR instr(lower(title), lower($needle)) > 0)");
                search.Parameters.AddWithValue("$needle", query);
            }
        }
        if (pinned.HasValue)
        {
            sql.Append(" AND pinned = $pinned");
            search.Parameters.AddWithValue("$pinned", pinned.Value ? 1 : 0);
        }
        if (excludePinned)
        {
            sql.Append(" AND pinned = 0");
        }
        if (!string.IsNullOrEmpty(tag))
        {
            sql.Append(" AND tag = $tag");
            search.Parameters.AddWithValue("$tag", tag);
        }
        if (excludeTagged)
        {
            sql.Append(" AND tag IS NULL");
        }
        if (kind.HasValue)
        {
            sql.Append(" AND type = $type");
            search.Parameters.AddWithValue("$type", kind.Value.ToString());
        }
        sql.Append(" ORDER BY pinned DESC, datetime DESC");
        search.CommandText = sql.ToString();
        using var reader = search.ExecuteReader();
        return ReadAll(reader);
    }

    public void RefreshDate(long id, DateTime utcNow)
    {
        using var touch = _connection.CreateCommand();
        touch.CommandText = "UPDATE clipboard SET datetime = $datetime WHERE id = $id";
        touch.Parameters.AddWithValue("$datetime", Stamp(utcNow));
        touch.Parameters.AddWithValue("$id", id);
        touch.ExecuteNonQuery();
    }

    public bool Delete(long id)
    {
        using var delete = _connection.CreateCommand();
        delete.CommandText = "DELETE FROM clipboard WHERE id = $id";
        delete.Parameters.AddWithValue("$id", id);
        return delete.ExecuteNonQuery() > 0;
    }

    public void SetPinned(long id, bool pinned) => SetField(id, "pinned", pinned ? 1 : 0);

    public void SetTag(long id, string? tag) =>
        SetField(id, "tag", (object?)tag ?? DBNull.Value);

    public void SetTitle(long id, string? title) =>
        SetField(id, "title", (object?)title ?? DBNull.Value);

    public void SetMetadata(long id, string? metadataJson) =>
        SetField(id, "metadata", (object?)metadataJson ?? DBNull.Value);

    public void SetMetadataAndTitle(long id, string? metadataJson, string? title)
    {
        using var update = _connection.CreateCommand();
        update.CommandText = "UPDATE clipboard SET metadata = $metadata, title = $title WHERE id = $id";
        update.Parameters.AddWithValue("$metadata", (object?)metadataJson ?? DBNull.Value);
        update.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
    }

    private void SetField(long id, string column, object value)
    {
        using var update = _connection.CreateCommand();
        update.CommandText = $"UPDATE clipboard SET {column} = $value WHERE id = $id";
        update.Parameters.AddWithValue("$value", value);
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _connection.Dispose();
    }

    private ClipboardItem? ReadById(long id)
    {
        using var query = _connection.CreateCommand();
        query.CommandText = $"""
            SELECT id, type, content, pinned, tag, datetime, {MetadataColumn}, title
            FROM clipboard WHERE id = $id
            """;
        query.Parameters.AddWithValue("$id", id);
        using var reader = query.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    private static List<ClipboardItem> ReadAll(SqliteDataReader reader)
    {
        var items = new List<ClipboardItem>();
        while (reader.Read())
        {
            // Rows of an unknown future type are skipped, never fatal: a newer
            // app version must not break listing/searching old installs.
            if (ReadItem(reader) is { } item)
            {
                items.Add(item);
            }
        }
        return items;
    }
    private string? ReadMetadata(long id)
    {
        using var read = _connection.CreateCommand();
        read.CommandText = $"SELECT {MetadataColumn} FROM clipboard WHERE id = $id";
        read.Parameters.AddWithValue("$id", id);
        return read.ExecuteScalar() as string;
    }

    private long? FindId(ItemKind kind, string content)
    {
        using var find = _connection.CreateCommand();
        find.CommandText = "SELECT id FROM clipboard WHERE type = $type AND content = $content LIMIT 1";
        find.Parameters.AddWithValue("$type", kind.ToString());
        find.Parameters.AddWithValue("$content", content);
        var result = find.ExecuteScalar();
        return result is long id ? id : null;
    }

    // First-run robustness: Validate() flags a missing directory as a UI
    // hint, but opening the store itself must succeed by creating the
    // parent. :memory: and in-memory modes are untouched.
    private static void EnsureParentDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode == SqliteOpenMode.Memory
            || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var parent = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private void EnsureSchema()
    {
        using var ddl = _connection.CreateCommand();
        ddl.CommandText = """
            CREATE TABLE IF NOT EXISTS clipboard (
              id       INTEGER PRIMARY KEY AUTOINCREMENT,
              type     TEXT    NOT NULL,
              content  TEXT    NOT NULL,
              pinned   INTEGER NOT NULL DEFAULT 0,
              tag      TEXT    NULL,
              datetime TEXT    NOT NULL,
              metadata TEXT    NULL,
              title    TEXT    NULL,
              UNIQUE (type, content)
            );
            CREATE TABLE IF NOT EXISTS clipboard_version (
              id      INTEGER PRIMARY KEY CHECK (id = 1),
              version INTEGER NOT NULL
            );
            INSERT INTO clipboard_version (id, version) VALUES (1, 2)
              ON CONFLICT (id) DO NOTHING;
            CREATE INDEX IF NOT EXISTS idx_clipboard_datetime ON clipboard (datetime DESC);
            CREATE INDEX IF NOT EXISTS idx_clipboard_pinned_datetime ON clipboard (pinned DESC, datetime DESC);
            CREATE INDEX IF NOT EXISTS idx_clipboard_protect ON clipboard (pinned, tag);
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            """;
        ddl.ExecuteNonQuery();
        MigrateV1TitleIfNeeded();
        NormalizeStoredDates();
    }

    // v0 databases lack the title column: add it and stamp version 2.
    private void MigrateV1TitleIfNeeded()
    {
        using var columns = _connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(clipboard)";
        using var reader = columns.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "title")
            {
                return;
            }
        }
        using var migrate = _connection.CreateCommand();
        migrate.CommandText = """
            ALTER TABLE clipboard ADD COLUMN title TEXT NULL;
            UPDATE clipboard_version SET version = 2 WHERE id = 1;
            """;
        migrate.ExecuteNonQuery();
    }

    private static string UnprotectedWhere(bool protectPinned = true, bool protectTagged = true)
    {
        var clauses = new List<string>();
        if (protectPinned && protectTagged)
        {
            return "NOT (pinned = 1 OR tag IS NOT NULL)";
        }
        if (protectPinned)
        {
            clauses.Add("pinned = 1");
        }
        if (protectTagged)
        {
            clauses.Add("tag IS NOT NULL");
        }
        return clauses.Count == 0 ? "" : $"NOT ({string.Join(" OR ", clauses)})";
    }

    private static void Bind(SqliteCommand command, ClipboardItem item)
    {
        command.Parameters.AddWithValue("$type", item.Kind.ToString());
        command.Parameters.AddWithValue("$content", item.Content);
        command.Parameters.AddWithValue("$pinned", item.Pinned ? 1 : 0);
        command.Parameters.AddWithValue("$tag", (object?)item.Tag ?? DBNull.Value);
        command.Parameters.AddWithValue("$datetime", Stamp(item.CapturedAt));
        command.Parameters.AddWithValue("$metadata", (object?)item.MetadataJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
    }

    private static readonly string[] StampFormats = ["yyyy-MM-dd HH:mm:ss.fffffff", "yyyy-MM-dd HH:mm:ss"];

    // Unreadable rows (an unknown future type, a date the reader cannot
    // parse after a hand edit or a migration) are skipped: one used to make
    // every read throw, and the popup never opened again.
    private static ClipboardItem? ReadItem(SqliteDataReader reader)
    {
        if (!Enum.TryParse<ItemKind>(reader.GetString(1), out var kind))
        {
            return null;
        }
        if (reader.IsDBNull(5))
        {
            return null;
        }
        var stamp = reader.GetString(5);
        if (!TryReadStamp(stamp, out var capturedAt) && !TryReadLegacyStamp(stamp, out capturedAt))
        {
            return null;
        }
        return new ClipboardItem(
            Kind: kind,
        Content: reader.GetString(2),
        Pinned: reader.GetInt64(3) == 1,
        Tag: reader.IsDBNull(4) ? null : reader.GetString(4),
        CapturedAt: capturedAt,
        MetadataJson: CoerceMetadata(reader.IsDBNull(6) ? null : reader.GetString(6)),
        Title: reader.IsDBNull(7) ? null : reader.GetString(7),
        Id: reader.GetInt64(0));
    }

    // Corrupt metadata degrades to null (json_valid in the query): content
    // is user data, metadata is enrichment. Never drop the item for a
    // broken enrichment payload.
    private static string? CoerceMetadata(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || raw.AsSpan().Trim().SequenceEqual("null") ? null : raw;

    // Test seam: raw SQL against this store (corrupt-row scenarios).
    internal void ExecuteForTests(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string EscapeLike(string query) => query
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    // Invariant: with the current culture a '.' time separator (fi-FI,
    // da-DK, a custom Windows setting) or another calendar (th-TH, fa-IR)
    // went into the database, and the reader could not read it back.
    private static string Stamp(DateTime capturedAt) =>
        capturedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);

    private const System.Globalization.DateTimeStyles StampStyles =
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal;

    private static bool TryReadStamp(string raw, out DateTime value) =>
        DateTime.TryParseExact(raw, StampFormats, System.Globalization.CultureInfo.InvariantCulture, StampStyles, out value);

    // A stamp an earlier version wrote under a culture with another time
    // separator or calendar, read the way that culture meant it. Only rows
    // the invariant reading rejects, or dates it puts centuries off.
    private static bool TryReadLegacyStamp(string raw, out DateTime value) =>
        DateTime.TryParseExact(raw, StampFormats, System.Globalization.CultureInfo.CurrentCulture, StampStyles, out value);

    private static bool IsPlausible(DateTime value) => value.Year is >= 1970 and <= 2200;

    // Rewrites legacy stamps once, so ordering, eviction and age limits
    // (all string comparisons in SQL) see one format again.
    private void NormalizeStoredDates()
    {
        var fixes = new List<(long Id, string Stamp)>();
        using (var query = _connection.CreateCommand())
        {
            query.CommandText = "SELECT id, datetime FROM clipboard";
            using var reader = query.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(1))
                {
                    continue;
                }
                var raw = reader.GetString(1);
                if (TryReadStamp(raw, out var canonical) && IsPlausible(canonical))
                {
                    continue;
                }
                if (TryReadLegacyStamp(raw, out var legacy) && IsPlausible(legacy))
                {
                    fixes.Add((reader.GetInt64(0), Stamp(legacy)));
                }
            }
        }
        if (fixes.Count == 0)
        {
            return;
        }
        using var transaction = _connection.BeginTransaction();
        using var update = _connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE clipboard SET datetime = $datetime WHERE id = $id";
        var stamp = update.Parameters.Add("$datetime", SqliteType.Text);
        var id = update.Parameters.Add("$id", SqliteType.Integer);
        foreach (var fix in fixes)
        {
            stamp.Value = fix.Stamp;
            id.Value = fix.Id;
            update.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
