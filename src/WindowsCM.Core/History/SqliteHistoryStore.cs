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

    public SqliteHistoryStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        EnsureSchema();
    }

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        var existing = FindId(item.Kind, item.Content);
        if (existing is long id)
        {
            using var bump = _connection.CreateCommand();
            bump.CommandText = """
                UPDATE clipboard SET datetime = $datetime, metadata = $metadata, title = $title
                WHERE id = $id
                """;
            bump.Parameters.AddWithValue("$datetime", Stamp(item.CapturedAt));
            bump.Parameters.AddWithValue("$metadata", (object?)item.MetadataJson ?? DBNull.Value);
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
        query.CommandText = """
            SELECT id, type, content, pinned, tag, datetime, metadata, title
            FROM clipboard
            ORDER BY datetime DESC
            """;
        using var reader = query.ExecuteReader();
        return ReadAll(reader);
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
        var sql = new System.Text.StringBuilder("""
            SELECT id, type, content, pinned, tag, datetime, metadata, title
            FROM clipboard WHERE 1 = 1
            """);
        if (!string.IsNullOrEmpty(query))
        {
            sql.Append(" AND (content LIKE $like ESCAPE '\\' COLLATE NOCASE");
            sql.Append(" OR title LIKE $like ESCAPE '\\' COLLATE NOCASE)");
            search.Parameters.AddWithValue("$like", $"%{EscapeLike(query)}%");
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
        sql.Append(" ORDER BY datetime DESC");
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
        query.CommandText = """
            SELECT id, type, content, pinned, tag, datetime, metadata, title
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
            items.Add(ReadItem(reader));
        }
        return items;
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
            CREATE INDEX IF NOT EXISTS idx_clipboard_protect ON clipboard (pinned, tag);
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            """;
        ddl.ExecuteNonQuery();
        MigrateV1TitleIfNeeded();
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

    private static ClipboardItem ReadItem(SqliteDataReader reader) => new(
        Kind: Enum.Parse<ItemKind>(reader.GetString(1)),
        Content: reader.GetString(2),
        Pinned: reader.GetInt64(3) == 1,
        Tag: reader.IsDBNull(4) ? null : reader.GetString(4),
        CapturedAt: DateTime.ParseExact(
            reader.GetString(5), StampFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal
                | System.Globalization.DateTimeStyles.AdjustToUniversal),
        MetadataJson: CoerceMetadata(reader.IsDBNull(6) ? null : reader.GetString(6)),
        Title: reader.IsDBNull(7) ? null : reader.GetString(7),
        Id: reader.GetInt64(0));

    // Corrupt metadata degrades to null: content is user data, metadata is
    // enrichment. Never drop the item for a broken enrichment payload.
    private static string? CoerceMetadata(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null")
        {
            return null;
        }
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(raw);
            return raw;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException
            || ex is NotSupportedException)
        {
            return null;
        }
    }

    private static string EscapeLike(string query) => query
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    private static string Stamp(DateTime capturedAt) =>
        capturedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff");
}
