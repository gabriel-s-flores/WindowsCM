// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WindowsCM.Core.History;

public enum HistoryOpenOutcome
{
    // The configured database opened normally.
    Opened,
    // It was damaged: kept aside (Detail = backup file name), a fresh one
    // took its place.
    RecoveredDamaged,
    // The configured location could not be used (unmounted drive, no
    // permission, locked): the default one is open (Detail = the path that
    // failed).
    FellBackToDefault,
    // Nothing could be opened: an in-memory history for this session.
    MemoryOnly,
}

public sealed record HistoryOpenResult(
    SqliteHistoryStore Store, HistoryOpenOutcome Outcome, string? Detail, Exception? Error);

// Opening the history never keeps the app from starting. Before this, a
// damaged database ("file is not a database" after a power loss), a
// custom location on a drive that is not mounted at logon or a file locked
// by another program threw out of startup on every launch: no tray icon,
// no message, only a line in the log.
public static class HistoryStoreOpener
{
    // SQLITE_CORRUPT and SQLITE_NOTADB: the file itself is unusable. Busy,
    // locked, I/O, permission and full-disk errors are not — moving the
    // file aside for those would lose a healthy history.
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    public static HistoryOpenResult Open(string path, string defaultPath, DateTime utcNow)
    {
        var (store, error, backup) = OpenOrRecover(path, utcNow);
        if (store is not null)
        {
            return backup is null
                ? new HistoryOpenResult(store, HistoryOpenOutcome.Opened, null, null)
                : new HistoryOpenResult(store, HistoryOpenOutcome.RecoveredDamaged, backup, error);
        }
        if (!SamePath(path, defaultPath))
        {
            var (fallback, _, _) = OpenOrRecover(defaultPath, utcNow);
            if (fallback is not null)
            {
                return new HistoryOpenResult(fallback, HistoryOpenOutcome.FellBackToDefault, path, error);
            }
        }
        return new HistoryOpenResult(
            new SqliteHistoryStore("Data Source=:memory:"), HistoryOpenOutcome.MemoryOnly, null, error);
    }

    public static string ConnectionStringFor(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

    private static (SqliteHistoryStore? Store, Exception? Error, string? Backup) OpenOrRecover(
        string path, DateTime utcNow)
    {
        if (TryOpen(path, out var store, out var error))
        {
            return (store, null, null);
        }
        if (error is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase }
            && TryMoveAside(path, utcNow, out var backup)
            && TryOpen(path, out store, out _))
        {
            return (store, error, Path.GetFileName(backup));
        }
        return (null, error, null);
    }

    private static bool TryOpen(string path, out SqliteHistoryStore? store, out Exception? error)
    {
        try
        {
            store = new SqliteHistoryStore(ConnectionStringFor(path));
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            store = null;
            error = ex;
            return false;
        }
    }

    // The WAL and shared-memory sidecars move with the file: a stale WAL
    // replayed into the fresh database would damage it again.
    private static bool TryMoveAside(string path, DateTime utcNow, out string backup)
    {
        backup = $"{path}.corrupt-{utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        try
        {
            for (var n = 2; File.Exists(backup); n++)
            {
                backup = $"{path}.corrupt-{utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{n}";
            }
            File.Move(path, backup);
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                if (File.Exists(path + suffix))
                {
                    File.Move(path + suffix, backup + suffix, overwrite: true);
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
